// Local-only runner. Secrets and tokens never enter the checked-in environment.
const fs = require('fs');
const path = require('path');
const crypto = require('crypto');
const newman = require('newman');
const root = path.resolve(__dirname, '../..');
const reports = path.join(root, 'reports/newman');
const secretsPath = path.join(__dirname, 'local.secrets.json');
const cases = ['CON-271','CON-485','CON-486','CON-487','CON-488','CON-489','CON-490','CON-491','CON-566','CON-567','CON-568','CON-569'];
async function main() {
  if (!fs.existsSync(secretsPath)) {
    fs.writeFileSync(secretsPath, JSON.stringify({jwtKey:crypto.randomBytes(48).toString('base64'),password:crypto.randomBytes(24).toString('base64url')+'!Aa1',adminEmail:'qa.newman.principal@example.test',adminPassword:crypto.randomBytes(24).toString('base64url')+'!Aa1'},null,2)+'\n');
    throw new Error('Generated ignored local.secrets.json. Start the APIs with npm run api:local first.');
  }
  const secrets = JSON.parse(fs.readFileSync(secretsPath, 'utf8').replace(/^\uFEFF/,''));
  if (!secrets.password || !secrets.adminEmail || !secrets.adminPassword) throw new Error('Missing local fixture password or bootstrap adminEmail/adminPassword in local.secrets.json');
  const environment = JSON.parse(fs.readFileSync(path.join(__dirname, 'local.environment.json'), 'utf8'));
  const values = Object.fromEntries(environment.values.map(v=>[v.key,v.value]));
  for (const k of ['baseUrl','baseUrlDotNet','baseUrlJava']) {
    if (!/^http:\/\/(localhost|127[.]0[.]0[.]1)(:[0-9]+)?$/.test(values[k])) throw new Error('Only loopback URLs allowed: '+k);
  }
  if (values.baseUrl !== values.baseUrlDotNet) throw new Error('baseUrl must equal baseUrlDotNet');
  async function api(route, method='GET', body, token) {
    const response = await fetch(values.baseUrlDotNet+route, {method,redirect:'error',signal:AbortSignal.timeout(30000),headers:{'Content-Type':'application/json',...(token?{Authorization:'Bearer '+token}:{})},...(body?{body:JSON.stringify(body)}:{})});
    const text=await response.text();
    let json; try {json=JSON.parse(text);} catch {json=null;}
    return {status:response.status,json};
  }
  const health=await api('/health');
  if (health.status!==200 || health.json?.status!=='OK') throw new Error('.NET health failed');
  const javaHealth=await fetch(values.baseUrlJava+'/api/health',{redirect:'error',signal:AbortSignal.timeout(30000)});
  if (javaHealth.status!==200 || (await javaHealth.json()).status!=='UP') throw new Error('Java local datasource health failed');
  const bootstrap=await api('/api/users/login','POST',{correo:secrets.adminEmail,password:secrets.adminPassword});
  if (bootstrap.status!==200 || bootstrap.json?.usuario?.rol!=='AdministradorPrincipal') throw new Error('Local bootstrap login failed. Start api:local against isolated ConnectaOficiosNewman database.');
  for (const [prefix,name,role] of [['cliente','Cliente',1],['trabajador','Trabajador',2],['admin','Administrador',3],['adminprincipal','AdministradorPrincipal',4]]) {
    const correo=prefix+'.test@connectaoficios.local';
    const login=await api('/api/users/login','POST',{correo,password:secrets.password});
    if (login.status===200) {
      if (login.json.usuario.rolId!==role) throw new Error('Unexpected role for local fixture '+prefix);
      continue;
    }
    if (login.status!==401) throw new Error('Unexpected fixture login HTTP '+login.status);
    const created=await api(role<=2?'/api/users/register':'/api/admin/accounts/','POST',{nombre:name+' local',correo,password:secrets.password,rolId:role},role<=2?null:bootstrap.json.token);
    if (created.status!==201) throw new Error('Could not create local fixture '+prefix+' (HTTP '+created.status+'). Existing accounts are never reset or overwritten.');
  }
  environment.values = environment.values.filter(v=>v.key!=='password');
  environment.values.push({key:'password',value:secrets.password,enabled:true,type:'secret'});
  fs.mkdirSync(reports,{recursive:true});
  const rawJson=path.join(reports,'newman-report.json');
  const startedAt=Date.now();
  const summary=await new Promise((resolve,reject)=>newman.run({
    collection:require('./local.collection.json'),environment,
    reporters:['cli','json','htmlextra'],timeoutRequest:30000,
    reporter:{json:{export:rawJson},htmlextra:{export:path.join(reports,'newman-report.html'),skipSensitiveData:true,title:'ConnectaOficios - Angel Mate - API Testing',titleSize:4,browserTitle:'ConnectaOficios - Newman',testPaging:false,showOnlyFails:false,displayProgressBar:true}}
  },(error,result)=>error?reject(error):resolve(result)));
  // Built-in Newman JSON report, reduced to execution evidence. Drop all request/
  // response bodies, headers, environments, cookies and streams (also byte buffers).
  const report=JSON.parse(fs.readFileSync(rawJson,'utf8'));
  const cleanError=e=>e?{name:e.name,message:e.message,index:e.index,test:e.test}:undefined;
  report.collection={info:{name:summary.collection.name,description:'Angel Mate cases; original general requests preserved. Local .NET SQL Express; Java H2 MODE=MySQL. Fresh HTTP integration run with genuine .NET JWTs.'}};
  delete report.environment;delete report.globals;
  report.run.executions=report.run.executions.map(e=>({cursor:e.cursor,item:{name:e.item.name},request:{method:e.request.method,url:e.request.url},response:e.response?{code:e.response.code,status:e.response.status,responseTime:e.response.responseTime}:undefined,assertions:(e.assertions||[]).map(a=>({assertion:a.assertion,skipped:a.skipped,error:cleanError(a.error)})),requestError:cleanError(e.requestError)}));
  report.run.failures=report.run.failures.map(f=>({error:cleanError(f.error),at:f.at,source:{name:f.source?.name},cursor:f.cursor}));
  const results=Object.fromEntries(cases.map(id=>{
    const executions=summary.run.executions.filter(e=>(e.assertions||[]).some(a=>a.assertion.includes(id)) || e.item.name.startsWith(id));
    return [id, executions.length>0 && executions.every(e=>!e.requestError && (e.assertions||[]).length>0 && e.assertions.every(a=>!a.error&&!a.skipped))?'Pass':'Fail'];
  }));
  const stats=summary.run.stats;
  const metrics={iterations:stats.iterations.total,requests:stats.requests.total,testScripts:stats.testScripts.total,assertions:stats.assertions.total,passed:stats.assertions.total-stats.assertions.failed,failedRequests:stats.requests.failed,failedAssertions:stats.assertions.failed,failures:summary.run.failures.length,executionTimeMs:summary.run.timings.completed-summary.run.timings.started,cases:results};
  report.apiTesting={generatedAt:new Date().toISOString(),startedAt:new Date(startedAt).toISOString(),databases:{dotnet:'SQL Server Express: isolated ConnectaOficiosNewman',java:'Existing H2 test datasource MODE=MySQL; local,test profiles'},metrics};
  fs.writeFileSync(rawJson,JSON.stringify(report,null,2)+'\n');
  console.log(JSON.stringify(metrics,null,2));
  if (stats.iterations.total!==1 || stats.requests.failed || stats.assertions.failed || summary.run.failures.length || Object.values(results).some(v=>v!=='Pass')) process.exitCode=1;
  if (!fs.existsSync(path.join(reports,'newman-report.html'))) throw new Error('Newman HTML missing');
}
main().catch(error=>{console.error(error.message);process.exitCode=1;});
