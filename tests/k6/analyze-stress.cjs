// Analyze native k6 JSON samples by the stress ramp's configured VU stages.
const fs = require('node:fs');
const path = require('node:path');
const root = path.resolve(__dirname, '../..');
const reportPath = path.join(root, 'reports/k6/stress-summary.json');
const seriesPath = path.join(root, 'reports/k6/stress-timeseries.json');
const summary = JSON.parse(fs.readFileSync(reportPath, 'utf8').replace(/^\uFEFF/, ''));
const samples = fs.readFileSync(seriesPath, 'utf8').split(/\r?\n/).filter(Boolean).map(line => JSON.parse(line)).filter(x => x.type === 'Point');
const rampStart = Math.min(...samples.filter(s => s.metric === 'case_560_duration' && s.data.tags?.case === 'CON-560').map(s => Date.parse(s.data.time)));
if (!Number.isFinite(rampStart)) throw Error('No progressive authentication samples in k6 output.');
const stages = [
  { start: 0, end: 10, vus: 1 }, { start: 10, end: 25, vus: 3 },
  { start: 25, end: 40, vus: 5 }, { start: 40, end: 55, vus: 8 },
  { start: 55, end: 70, vus: 10 }, { start: 70, end: 105, vus: 12 },
];
const percentile = (values, p) => {
  if (!values.length) return null;
  values.sort((a,b)=>a-b);
  return values[Math.min(values.length-1, Math.ceil(values.length*p)-1)];
};
const stageMetrics = stages.map(stage => {
  const selected = samples.filter(s => {
    if (!/^case_\d+_(duration|success|requests)$/.test(s.metric)) return false;
    const tags=s.data.tags||{}; if(tags.scenario && tags.scenario!=='progressive_stress') return false;
    const delta=(Date.parse(s.data.time)-rampStart)/1000;
    return delta>=stage.start && delta<stage.end;
  });
  const duration=selected.filter(s=>s.metric.endsWith('_duration')).map(s=>Number(s.data.value)).filter(Number.isFinite);
  const success=selected.filter(s=>s.metric.endsWith('_success')).map(s=>Number(s.data.value)).filter(Number.isFinite);
  const requestSamples=selected.filter(s=>s.metric.endsWith('_requests')).length;
  return {
    intervalSeconds:`${stage.start}-${stage.end}`,
    targetVUs:stage.vus,
    requests:requestSamples,
    requestsPerSecond:requestSamples/(stage.end-stage.start),
    errorRate:success.length?1-success.reduce((a,b)=>a+b,0)/success.length:null,
    avgMs:duration.length?duration.reduce((a,b)=>a+b,0)/duration.length:null,
    p95Ms:percentile(duration,0.95),p99Ms:percentile(duration,0.99),
    sampleCount:duration.length,
  };
});
const baseline=stageMetrics[0];
let degradation=stageMetrics.slice(1).find((stage,index)=>{
  const previous=stageMetrics[index];
  if(stage.sampleCount<10||!baseline.p95Ms)return false;
  const latencyRise=stage.p95Ms>=baseline.p95Ms*1.3;
  const severeLatency=stage.p95Ms>=baseline.p95Ms*2;
  const errorRise=stage.errorRate!==null && stage.errorRate>=(baseline.errorRate||0)+0.01;
  const throughputDrop=previous.requestsPerSecond>0 && stage.requestsPerSecond/stage.vus<previous.requestsPerSecond/previous.targetVUs*0.7;
  return severeLatency || errorRise || (latencyRise&&throughputDrop);
});
summary.metadata.stageResults=stageMetrics;
summary.metadata.degradation={
  criterion:'Compared with 1-VU baseline: p95 >=2x, or error rate rises by >=1 percentage point, or p95 rises >=30% while per-VU throughput falls >=30%. Descriptive observation, not an SLA.',
  point:degradation?`${degradation.targetVUs} VUs (stage ${degradation.intervalSeconds}s)`:null,
  determined:!!degradation,
  baseline,
  analysis:'Native k6 JSON output samples; only progressive_stress scenarios, grouped by configured stage time.',
  endpointWithHighestP95:summary.metadata.endpoints?.[0]?.endpoint||null,
  endpointRanking:summary.metadata.endpoints||[],
};
const c565=summary.metadata.cases.find(x=>x.id==='CON-565');
if(c565)c565.status=summary.metadata.endpoints?.length?'Pass':'Fail';
fs.writeFileSync(reportPath,JSON.stringify(summary,null,2)+'\n');
console.log(JSON.stringify({stages:stageMetrics,degradation:summary.metadata.degradation.point,endpointWithHighestP95:summary.metadata.degradation.endpointWithHighestP95},null,2));
