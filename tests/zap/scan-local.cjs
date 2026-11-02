// Angel Mate: CON-518..526. Requires the existing isolated api:local environment.
// No third-party dependencies. Credentials are read only from the ignored local file.
const fs = require('node:fs');
const path = require('node:path');
const crypto = require('node:crypto');
const root = path.resolve(__dirname, '../..');
const out = path.join(root, 'reports/zap');
const dn = 'http://127.0.0.1:8080', ja = 'http://127.0.0.1:8081';
const zap = 'http://127.0.0.1:8090';
const sleep = ms => new Promise(r => setTimeout(r, ms));
const secret = JSON.parse(fs.readFileSync(process.env.ZAP_QA_SECRETS || path.join(root, 'tests/postman/local.secrets.json'), 'utf8').replace(/^\uFEFF/, ''));
secret.adminEmail ||= secret.correo;
secret.adminPassword ||= secret.password;
if(['adminEmail','adminPassword','password','jwtKey'].some(k=>!secret[k])) throw Error('QA credentials file is incomplete');
const evidence = [], results = {}, scans = [];
const started = Date.now();
function local(url) {
  const u = new URL(url);
  if (![dn, ja].includes(u.origin) || u.username || u.password) throw Error('Non-local target rejected');
  return u;
}
async function z(component, operation, name, params = {}, format = 'JSON') {
  const u = new URL(`${zap}/${format}/${component}/${operation}/${name}/`);
  const response = await fetch(u, {method:'POST', headers:{'Content-Type':'application/x-www-form-urlencoded'}, body:new URLSearchParams(params).toString(), signal:AbortSignal.timeout(180000)});
  const text = await response.text();
  if (!response.ok) throw Error(`ZAP ${component}/${name}: HTTP ${response.status}`);
  if (format !== 'JSON') return text;
  const j = JSON.parse(text);
  if (j.code) throw Error(`ZAP ${component}/${name}: ${j.code} ${j.message}`);
  return j;
}
async function http(url, method = 'GET', body, token, throughZap = false, extraHeaders = {}) {
  const u = local(url), data = body === undefined ? '' : JSON.stringify(body);
  let status, text, headers;
  if (throughZap) {
    const raw = `${method} ${u.pathname}${u.search} HTTP/1.1\r\nHost: ${u.host}\r\nContent-Type: application/json\r\n${Object.entries(extraHeaders).map(([k,v])=>k+': '+v+'\r\n').join('')}${token ? 'Authorization: Bearer '+token+'\r\n' : ''}${data ? 'Content-Length: '+Buffer.byteLength(data)+'\r\n' : ''}\r\n${data}`;
    const j = await z('core','action','sendRequest',{request:raw,followRedirects:'false'});
    const message = j.sendRequest[0];
    if (!message) throw Error('ZAP did not return an HTTP message');
    status = Number(/^HTTP\/\S+\s+(\d{3})/.exec(message.responseHeader)?.[1]); text = message.responseBody;
    headers = Object.fromEntries(message.responseHeader.split('\r\n').slice(1).filter(s=>s.includes(':')).map(s=>[s.slice(0,s.indexOf(':')).toLowerCase(),s.slice(s.indexOf(':')+1).trim()]));
  } else {
    const r = await fetch(url,{method,redirect:'error',signal:AbortSignal.timeout(180000),headers:{'Content-Type':'application/json',...(token?{Authorization:'Bearer '+token}:{})},...(data?{body:data}:{})});
    status=r.status; text=await r.text(); headers=Object.fromEntries(r.headers);
  }
  let json; try { json=JSON.parse(text); } catch { json=null; }
  // Never retain request headers, credentials or complete response bodies in evidence.
  evidence.push({method,url,status,viaZap:throughZap,headers:{...headers,'set-cookie':undefined}});
  return {status,json,text,headers};
}
function expect(r, status, label) {
  if (![].concat(status).includes(r.status)) throw Error(`${label}: HTTP ${r.status}; expected ${status}`);
  return r.json;
}
async function login(email,password) {
  const j=expect(await http(dn+'/api/users/login','POST',{correo:email,password}),200,'QA login');
  return {id:j.usuario.id,token:j.token,role:j.usuario.rol};
}
async function account(run, suffix, rolId) {
  const email=`zap.${run}.${suffix}@example.test`;
  expect(await http(dn+'/api/users/register','POST',{nombre:`QA ZAP ${suffix}`,correo:email,password:secret.password,rolId}),201,'Register unique QA account');
  return login(email,secret.password);
}
function check(id, assertions) {
  results[id] = assertions.every(Boolean) ? 'Pass' : 'Fail';
  console.log(id+': '+results[id]);
}
async function functional(iteration) {
  const run = crypto.randomUUID();
  const principal = await login(secret.adminEmail,secret.adminPassword);
  if (principal.role !== 'AdministradorPrincipal') throw Error('QA bootstrap role mismatch');
  const a = await account(run,'cliente-a',1), b = await account(run,'cliente-b',1);
  const worker = await account(run,'trabajador-a',2), outsider = await account(run,'trabajador-b',2);
  const me = dn+'/api/users/'+a.id;
  expect(await http(me,'GET',undefined,a.token),200,'Valid JWT control');
  const [head,payload] = a.token.split('.');
  const claims = JSON.parse(Buffer.from(payload,'base64url'));
  const expiredPayload = Buffer.from(JSON.stringify({...claims,nbf:Math.floor(Date.now()/1000)-7200,iat:Math.floor(Date.now()/1000)-7200,exp:Math.floor(Date.now()/1000)-3600})).toString('base64url');
  const unsigned = head+'.'+expiredPayload;
  const expired = unsigned+'.'+crypto.createHmac('sha256',secret.jwtKey).update(unsigned).digest('base64url');
  const expiredResponse = await http(me,'GET',undefined,expired,true);
  // Same signed claims with a future expiry must be accepted, isolating expiry from key/config errors.
  const controlPayload=Buffer.from(JSON.stringify({...claims,exp:Math.floor(Date.now()/1000)+3600})).toString('base64url');
  const controlUnsigned=head+'.'+controlPayload;
  const control=controlUnsigned+'.'+crypto.createHmac('sha256',secret.jwtKey).update(controlUnsigned).digest('base64url');
  const signedControl=await http(me,'GET',undefined,control);
  check('CON-518',[signedControl.status===200,expiredResponse.status===401,!expiredResponse.json?.id,!expiredResponse.text.includes('example.test')]);
  const parts=a.token.split('.'); parts[2]=(parts[2][0]==='A'?'B':'A')+parts[2].slice(1);
  const invalid=await http(me,'GET',undefined,parts.join('.'),true);
  check('CON-519',[invalid.status===401,!invalid.json?.id,!invalid.text.includes('example.test')]);
  const createAdmin={nombre:'QA denied creation',correo:`denied.${run}@example.test`,password:secret.password,rolId:3};
  // Do not proxy credential-bearing bodies into ZAP history/reports.
  const adminEmail=`zap.${run}.admin@example.test`;
  expect(await http(dn+'/api/admin/accounts/','POST',{nombre:'QA ZAP lower admin',correo:adminEmail,password:secret.password,rolId:3},principal.token),201,'Create lower QA admin');
  const lower=await login(adminEmail,secret.password);
  if(lower.role!=='Administrador') throw Error('Lower administrative role fixture missing');
  const denied=await http(dn+'/api/admin/accounts/','POST',createAdmin,lower.token);
  const principalControl=await http(dn+'/api/admin/accounts?correo='+encodeURIComponent(createAdmin.correo),'GET',undefined,principal.token);
  const absent=principalControl.json?.items?.every(u=>u.correo!==createAdmin.correo);
  check('CON-520',[denied.status===403,principalControl.status===200,absent===true]);
  const cat=expect(await http(ja+'/api/categorias','POST',{nombre:'QA ZAP '+run,descripcion:'Angel local QA'},principal.token),201,'Category');
  const zone=expect(await http(ja+'/api/zonas-cobertura','POST',{departamento:'QA ZAP',municipio:run,localidad:'Isolated test'},principal.token),201,'Zone');
  const profile=expect(await http(ja+'/api/perfiles-trabajador','POST',{oficioPrincipal:'QA ZAP',descripcionProfesional:run,zonaPrincipalId:zone.id},worker.token),201,'Profile');
  const serviceBody={perfilTrabajadorId:profile.id,categoriaId:cat.id,titulo:'QA ZAP '+run,descripcion:'Exclusive local security fixture',tarifaMinima:1,tarifaMaxima:2,zonasCoberturaIds:[zone.id]};
  const service=expect(await http(ja+'/api/servicios','POST',serviceBody,worker.token,true),201,'Service');
  expect(await http(ja+`/api/servicios/${service.id}`,'GET',undefined,worker.token,true),200,'Own service GET');
  expect(await http(ja+`/api/servicios/${service.id}`,'PUT',{titulo:serviceBody.titulo+' updated',descripcion:'QA only',tarifaMinima:1},worker.token,true),200,'Own service PUT');
  const disposable=expect(await http(ja+'/api/servicios','POST',{...serviceBody,titulo:'Disposable '+run},worker.token,true),201,'Disposable service');
  expect(await http(ja+`/api/servicios/${disposable.id}`,'DELETE',undefined,worker.token,true),[200,204],'Own service DELETE');
  const solicitation=expect(await http(ja+'/api/solicitudes','POST',{servicioId:service.id,clienteId:a.id,trabajadorId:worker.id,fechaPropuesta:new Date(Date.now()+86400000*2).toISOString().slice(0,10),horaAproximada:'10:00:00',direccionServicio:'QA local fixture',descripcionTrabajo:'QA ZAP '+run},a.token,true),201,'Solicitud');
  solicitation.id=solicitation.idSolicitud;
  if(!Number.isInteger(solicitation.id)) throw Error('Missing real idSolicitud');
  const solicitationUrl=ja+`/api/solicitudes/${solicitation.id}`;
  const before=await http(solicitationUrl,'GET',undefined,a.token,true);
  const attempt=await http(solicitationUrl+'/aceptar','PATCH',undefined,outsider.token,true);
  const after=await http(solicitationUrl,'GET',undefined,a.token,true);
  const ownerAccept=await http(solicitationUrl+'/aceptar','PATCH',undefined,worker.token,true);
  check('CON-522',[before.status===200,attempt.status===403,after.status===200,before.json?.estado==='PENDIENTE',after.json?.estado===before.json?.estado,ownerAccept.status===200]);
  const conv=expect(await http(ja+'/api/conversaciones','POST',{solicitudId:solicitation.id},a.token),201,'Conversation');
  const owner=await http(ja+`/api/conversaciones/${conv.id}`,'GET',undefined,a.token,true);
  const notParticipant=await http(ja+`/api/conversaciones/${conv.id}`,'GET',undefined,b.token,true);
  check('CON-521',[owner.status===200,notParticipant.status===400,notParticipant.json?.mensaje==='El usuario no pertenece a esta conversación',!notParticipant.json?.solicitudId,!notParticipant.json?.id]);
  const snapshot={iteration,run,results:{...results},checks:{'CON-518':{validSignedControl:signedControl.status,expired:expiredResponse.status,protectedDataReturned:false},'CON-519':{invalidSignature:invalid.status,protectedDataReturned:false},'CON-520':{lowerRole:lower.role,forbiddenOperation:denied.status,principalControl:principalControl.status,accountNotCreated:absent},'CON-521':{participant:owner.status,nonParticipant:notParticipant.status,message:notParticipant.json?.mensaje,protectedDataReturned:!!notParticipant.json?.solicitudId},'CON-522':{nonParticipant:attempt.status,before:before.json?.estado,after:after.json?.estado,ownerControl:ownerAccept.status}},fixtures:{clienteA:a.id,clienteB:b.id,trabajador:worker.id,noParticipante:outsider.id,servicio:service.id,solicitud:solicitation.id,conversacion:conv.id}};
  fs.writeFileSync(path.join(out,`functional-${iteration}.json`),JSON.stringify(snapshot,null,2));
  if(Object.values(results).includes('Fail')) throw Error('Functional security assertion failed');
  return {worker,a,service,solicitation};
}
async function passiveDrain() {
  const deadline=Date.now()+180000;
  while(Number((await z('pscan','view','recordsToScan')).recordsToScan)>0) {
    if(Date.now()>deadline) throw Error('Passive scan did not finish');
    await sleep(1000);
  }
}
async function main() {
  fs.mkdirSync(out,{recursive:true});
  const version=(await z('core','view','version')).version;
  console.log('OWASP ZAP '+version);
  const existingRules=(await z('replacer','view','rules')).rules;
  if(existingRules.some(r=>r.description==='QA Java bearer')) await z('replacer','action','removeRule',{description:'QA Java bearer'});
  await z('core','action','newSession',{name:path.join(out,'runtime','angel-final-'+Date.now()),overwrite:'false'});
  const ctx=(await z('context','action','newContext',{contextName:'Angel local QA'})).contextId;
  await z('context','action','includeInContext',{contextName:'Angel local QA',regex:'http://127[.]0[.]0[.]1:(8080|8081)/.*'});
  await z('context','action','setContextInScope',{contextName:'Angel local QA',booleanInScope:'true'});
  // Guard ZAP itself from following external redirects/URLs, in addition to our URL allowlist.
  await z('core','action','excludeFromProxy',{regex:'^(?!http://127[.]0[.]0[.]1:(8080|8081)/).*$'});
  await functional(1);
  const second=await functional(2);
  console.log('Functional security repeated with new accounts and generated IDs.');
  const publicTargets=[dn+'/health',dn+'/swagger/v1/swagger.json',ja+'/api/health',ja+'/v3/api-docs'];
  for(const url of publicTargets) expect(await http(url,'GET',undefined,undefined,true),200,'Public target');
  // Explicit CORS preflight and untrusted Origin: record observable headers, never infer an exploit.
  await http(ja+'/api/health','OPTIONS',undefined,undefined,true,{Origin:'https://qa-untrusted.invalid','Access-Control-Request-Method':'GET'});
  // Scoped API scan from the REAL local contracts: only read operations and generated own IDs.
  for(const [base,contract,paths,token] of [
    [dn,'/swagger/v1/swagger.json',{'/health':'/health'},null],
    [ja,'/v3/api-docs',{'/api/health':'/api/health','/api/servicios/{id}':`/api/servicios/${second.service.id}`,'/api/solicitudes/{id}':`/api/solicitudes/${second.solicitation.id}`},second.worker.token]
  ]) {
    const spec=expect(await http(base+contract),200,'OpenAPI contract');
    const selected={};
    for(const [from,to] of Object.entries(paths)) {
      if(!spec.paths[from]?.get) throw Error('Missing GET in real OpenAPI: '+from);
      selected[to]={get:{...spec.paths[from].get,parameters:(spec.paths[from].get.parameters||[]).filter(p=>p.in!=='path')}};
    }
    spec.paths=selected; spec.servers=[{url:base}];
    // Strip contract links/examples to prevent accidental external access during import.
    delete spec.externalDocs;
    const file=path.join(out,base===dn?'dotnet-scope.openapi.json':'java-scope.openapi.json');
    fs.writeFileSync(file,JSON.stringify(spec));
    if(token) await z('replacer','action','addRule',{description:'QA Java bearer',enabled:'true',matchType:'REQ_HEADER',matchRegex:'false',matchString:'Authorization',replacement:'Bearer '+token,url:'http://127[.]0[.]0[.]1:8081/.*'});
    await z('openapi','action','importFile',{file,contextId:ctx});
  }
  // Focused local policy: no OAST, external callbacks, file/command reads or destructive methods.
  const policy='Angel local API';
  const policies=(await z('ascan','view','scanPolicyNames')).scanPolicyNames;
  if(policies.includes(policy)) await z('ascan','action','removeScanPolicy',{scanPolicyName:policy});
  await z('ascan','action','addScanPolicy',{scanPolicyName:policy});
  await z('ascan','action','disableAllScanners',{scanPolicyName:policy});
  const rules='40012,40018,40003,40008';
  await z('ascan','action','enableScanners',{ids:rules,scanPolicyName:policy});
  await z('ascan','action','setPolicyAttackStrength',{id:'0',attackStrength:'LOW',scanPolicyName:policy});
  await z('ascan','action','setOptionThreadPerHost',{Integer:'2'});
  await z('ascan','action','setOptionAddQueryParam',{Boolean:'true'});
  const searchUrl=ja+'/api/servicios/buscar?texto='+encodeURIComponent(second.service.titulo)+'&limit=10';
  const ownRequestsUrl=ja+`/api/solicitudes/trabajador/${second.worker.id}/paginadas?page=0&size=10`;
  expect(await http(searchUrl,'GET',undefined,second.worker.token,true),200,'Own service search');
  expect(await http(ownRequestsUrl,'GET',undefined,second.worker.token,true),200,'Own requests pagination');
  for(const url of [dn+'/health',ja+`/api/servicios/${second.service.id}`,ja+`/api/solicitudes/${second.solicitation.id}`,searchUrl,ownRequestsUrl]) {
    local(url);
    const scanId=(await z('ascan','action','scan',{url,recurse:'false',inScopeOnly:'true',scanPolicyName:policy,method:'GET',contextId:ctx})).scan;
    console.log('Active scan '+scanId+' '+url);
    const deadline=Date.now()+900000;
    while(Number((await z('ascan','view','status',{scanId})).status)<100) {
      if(Date.now()>deadline) throw Error('Active scan timeout; no successful result claimed');
      await sleep(2000);
    }
    const completed=(await z('ascan','view','scans')).scans.find(s=>s.id===scanId);
    const progress=await z('ascan','view','scanProgress',{scanId});
    if(completed?.state!=='FINISHED'||Number(completed.reqCount)===0) throw Error('No completed active requests');
    scans.push({scanId,url,status:100,method:'GET',requestCount:Number(completed.reqCount),state:completed.state,progress});
  }
  await passiveDrain();
  check('CON-523',[publicTargets.every(url=>evidence.some(e=>e.url===url&&e.viaZap&&e.status===200)),scans.some(s=>s.url===dn+'/health')]);
  check('CON-524',[['GET','POST','PUT','DELETE'].every(method=>evidence.some(e=>e.url.startsWith(ja+'/api/servicios')&&e.method===method&&e.viaZap&&e.status>=200&&e.status<300)),scans.some(s=>s.url.includes('/api/servicios/'))]);
  check('CON-525',[evidence.some(e=>e.url.startsWith(ja+'/api/solicitudes')&&e.viaZap&&e.status===201),scans.some(s=>s.url.includes('/api/solicitudes/'))]);
  check('CON-526',[evidence.some(e=>e.url===ja+'/api/health'&&e.viaZap&&e.headers['content-type'])]);
  const urls=(await z('core','view','urls')).urls;
  if(urls.some(url=>![dn,ja].includes(new URL(url).origin))) throw Error('Unexpected URL in ZAP session');
  const observedUrls=new Set(); let recordedMessages=0;
  for(let start=0;;start+=500) {
    const batch=(await z('core','view','messages',{start:String(start),count:'500'})).messages;
    for(const m of batch) if(/^HTTP\/\S+\s+[1-5]\d\d/.test(m.responseHeader)) {
      const url=m.requestHeader.split(' ')[1]; local(url); observedUrls.add(url); recordedMessages++;
    }
    if(batch.length<500) break;
  }
  const durationSeconds=Math.round((Date.now()-started)/1000);
  const endpointsApproximate=new Set([...observedUrls].map(u=>{const x=new URL(u);return x.origin+x.pathname;})).size;
  const description=`Fresh local QA execution ${new Date(started).toISOString()}. Angel Mate CON-518..526. Passive .NET/Java traffic; scoped OpenAPI import; focused active GET scans (XSS reflected, SQL injection, CRLF, parameter tampering). ${observedUrls.size} distinct URLs with actual HTTP responses, ${endpointsApproximate} paths including negative probes, ${recordedMessages} recorded messages, ${scans.reduce((n,s)=>n+s.requestCount,0)} active requests. Duration: ${durationSeconds} seconds (functional preparation through scan completion, excluding report export). Functional cases repeated twice: ${JSON.stringify(results)}. CON-521 rejects outsiders with HTTP 400 and no conversation data. No external targets/callbacks; no alert suppression. QA only, not an exhaustive audit.`;
  // Official ZAP reports, unfiltered: retain every severity and confidence.
  for(const [format,template] of [['html','traditional-html'],['json','traditional-json'],['xml','traditional-xml']]) {
    await z('reports','action','generate',{title:'ConnectaOficios - Angel Mate - OWASP ZAP',template,reportDir:out,reportFileName:'zap-report.'+format,description,display:'false'});
  }
  const report=JSON.parse(fs.readFileSync(path.join(out,'zap-report.json'),'utf8'));
  const counts={High:0,Medium:0,Low:0,Informational:0};
  const alerts=(report.site||[]).flatMap(s=>s.alerts||[]);
  for(const a of alerts) counts[['Informational','Low','Medium','High'][Number(a.riskcode)]]++;
  const summary={generatedAt:new Date().toISOString(),startedAt:new Date(started).toISOString(),version,targets:[dn,ja],scanType:'Passive + scoped OpenAPI import + focused Active GET scan',activeRules:rules,scans,counts,totalAlerts:alerts.length,alertInstances:alerts.reduce((s,a)=>s+Number(a.count||a.instances?.length||0),0),durationSeconds,urlsAnalyzed:observedUrls.size,endpointsApproximate,recordedMessages,results,limitations:['Existing local QA services: Java H2 MySQL mode; .NET isolated SQL Express. Not a production/MySQL parity test. Record the running service build/database in the execution notes.','Focused active policy; not an exhaustive security audit. No OAST, external callbacks, command/file-read or destructive active methods.','Pass for CON-523..526 means scan execution/evidence complete, not absence of vulnerabilities.','Counts are ZAP alert groups per site; instances reported separately. Paths include negative probes. Duration excludes report export.']};
  fs.writeFileSync(path.join(out,'summary.json'),JSON.stringify(summary,null,2));
  fs.writeFileSync(path.join(out,'http-evidence.json'),JSON.stringify(evidence,null,2));
  // Report export must never contain local passwords, signing keys or JWT strings.
  for(const filename of ['zap-report.html','zap-report.json','zap-report.xml','summary.json','http-evidence.json','functional-1.json','functional-2.json']) {
    const text=fs.readFileSync(path.join(out,filename),'utf8');
    if(fs.statSync(path.join(out,filename)).mtimeMs<started) throw Error('Stale report: '+filename);
    if([secret.password,secret.adminPassword,secret.jwtKey].some(s=>s&&text.includes(s))||/eyJ[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+/.test(text)) throw Error('Sensitive value found in '+filename);
  }
  console.log(JSON.stringify({...summary,scans:scans.map(({progress,...scan})=>scan)},null,2));
  if(Object.values(results).some(v=>v!=='Pass')) process.exitCode=1;
}
main().catch(e=>{console.error(e.message);process.exitCode=1;}).finally(async()=>{
  // Remove our in-memory bearer rule even on failure; existing unrelated rules are preserved.
  try { const rules=(await z('replacer','view','rules')).rules; if(rules.some(r=>r.description==='QA Java bearer')) await z('replacer','action','removeRule',{description:'QA Java bearer'}); } catch { process.exitCode=1; }
});
