// Only additions owned by Angel Mate. Original requests and scripts stay unchanged.
module.exports = function extend(collection) {
  collection.info.name = 'ConnectaOficios - API Testing';
  collection.info.description = 'Local API testing. Angel Mate: CON-271, CON-485..491, CON-566..569. Original general requests retain their scripts and are not reassigned. CON-271 reuses Login Cliente; CON-568 reuses the existing filtered pagination request. Run the complete collection in order. npm run api provisions the original local fixtures through official APIs; npm run api:local starts isolated SQL Express and Java using its existing H2 test datasource (MySQL mode), real JWT security and local image provider. This validates HTTP integration, not MySQL/production parity. No real users, external email, payments, or destructive cleanup. Import local.environment.json and supply password locally; never export runtime tokens.';
  for (const key of ['authToken','clienteToken','trabajadorToken','adminToken','userId','clienteId','trabajadorId','adminId','servicioId','planPromocionId','promocionId','categoriaId','zonaId','perfilTrabajadorId','runId','javaTrabajadorId','javaTrabajadorEmail','javaTrabajadorToken','relatedDotNetId']) {
    collection.variable.push({key,value:''});
  }
  const script = code => ({type:'text/javascript',exec:code.trim().split('\n')});
  collection.event.push({listen:'prerequest',script:script(`
if (pm.info.requestName === 'Login Cliente') {
  ['authToken','clienteToken','trabajadorToken','adminToken','userId','clienteId','trabajadorId','adminId','servicioId','planPromocionId','promocionId','categoriaId','zonaId','perfilTrabajadorId'].forEach(k => pm.collectionVariables.set(k, ''));
  pm.collectionVariables.set('runId', Date.now() + '-' + pm.variables.replaceIn('{{$guid}}'));
}
for (const key of ['baseUrlDotNet','baseUrlJava']) {
  if (!/^http:\\/\\/(localhost|127[.]0[.]0[.]1)(:[0-9]+)?$/.test(pm.variables.get(key))) {
    pm.execution.skipRequest(); throw new Error('Angel API suite requires local loopback URLs: ' + key);
  }
}
if (pm.variables.get('baseUrl') !== pm.variables.get('baseUrlDotNet')) {
  pm.execution.skipRequest(); throw new Error('baseUrl must equal baseUrlDotNet for the preserved requests');
}
`)});
  collection.event.push({listen:'test',script:script(`
// Supplemental Angel assertions; no original request script is edited.
if (pm.info.requestName === 'Registro rol 2') {
  pm.test('CON-485 - reutilizar identidad desechable ya creada por la coleccion',()=>{
    pm.response.to.have.status(201); const j=pm.response.json();
    pm.expect(j.id).to.be.a('number').above(0);pm.expect(j.rolId).eq(2);
    pm.collectionVariables.set('javaTrabajadorId',j.id);
    pm.collectionVariables.set('javaTrabajadorEmail',j.correo);
  });
}
if (pm.info.requestName.startsWith('Login ')) {
  pm.test('CON-271 - login JSON y JWT no vacio', () => {
    pm.response.to.have.status(200);
    pm.expect(pm.response.headers.get('Content-Type')).to.match(/application\\/json/i);
    const j = pm.response.json();
    pm.expect(j.token).to.be.a('string').and.match(/^[A-Za-z0-9_-]+[.][A-Za-z0-9_-]+[.][A-Za-z0-9_-]+$/);
    pm.expect(j.usuario.id).to.be.a('number').above(0);
    const payload = JSON.parse(atob(j.token.split('.')[1].replace(/-/g,'+').replace(/_/g,'/')));
    pm.expect(payload.sub).to.equal(String(j.usuario.id));
    pm.expect(payload['http://schemas.microsoft.com/ws/2008/06/identity/claims/role']).to.equal(j.usuario.rol);
    pm.expect(payload.exp).to.be.above(Date.now()/1000);
    if (pm.info.requestName === 'Login Cliente') {
      pm.collectionVariables.set('authToken', j.token);
      pm.collectionVariables.set('userId', j.usuario.id);
    }
  });
}
if (pm.info.requestName.startsWith('Paginacion /api/admin/users?')) {
  pm.test('CON-568 - paginacion y filtros reales compatibles', () => {
    pm.response.to.have.status(200);
    const j = pm.response.json();
    const url = pm.variables.replaceIn(pm.request.url.toString());
    const query = url.split('?')[1].split('&').reduce((a,p) => {const v=p.split('='); a[v[0]]=decodeURIComponent(v[1]);return a;},{});
    pm.expect(j.page).to.equal(Number(query.page));
    pm.expect(j.pageSize).to.equal(Number(query.pageSize));
    pm.expect(j.items).to.be.an('array');
    pm.expect(j.totalItems).to.be.a('number').at.least(j.items.length);
    pm.expect(j.totalPages).to.equal(Math.ceil(j.totalItems/j.pageSize));
    if (query.nombre) pm.expect(j.items.length, 'fixture esperado por filtros').above(0);
    j.items.forEach(u => {
      if (query.nombre) pm.expect(u.nombre.toLowerCase()).to.include(query.nombre.toLowerCase());
      if (query.correo) pm.expect(u.correo.toLowerCase()).to.include(query.correo.toLowerCase());
      if (query.rolId) pm.expect(u.rolId).to.equal(Number(query.rolId));
      if (query.estado) pm.expect(u.estado).to.equal('Activo');
      ['password','passwordHash','passwordResetToken','passwordResetTokenHash'].forEach(k => pm.expect(u).not.to.have.property(k));
    });
  });
}
`)});
  function request(name, method, url, status, token, body, tests='', pre='') {
    const exec = [
      `pm.test('${name} - HTTP ${status}', () => pm.response.to.have.status(${status}));`,
      ...(status === 200 || status === 201 ? ["pm.test('Content-Type JSON', () => pm.expect(pm.response.headers.get('Content-Type')).to.match(/application\\/json/i));"] : []),
      tests
    ].filter(Boolean).join('\n');
    return {name, request:{method,header:[{key:'Content-Type',value:'application/json'},...(token?[{key:'Authorization',value:`Bearer {{${token}}}`}]:[])],url,...(body?{body:{mode:'raw',raw:JSON.stringify(body),options:{raw:{language:'json'}}}}:{})},event:[...(pre?[{listen:'prerequest',script:script(pre)}]:[]),{listen:'test',script:script(exec)}]};
  }
  const folder = (name,item) => ({name,description:'Owner: Angel Mate. Only these added cases are assigned to Angel.',item});
  const dotnet='{{baseUrlDotNet}}', java='{{baseUrlJava}}';
  const publicFields = `
pm.test('CON-566 - identidad, contrato y ausencia de secretos', () => {
  const j=pm.response.json();
  ['id','nombre','correo','telefono','rolId','rol','estado','fechaCreacion'].forEach(k=>pm.expect(j).to.have.property(k));
  pm.expect(j.id).eq(Number(pm.collectionVariables.get('clienteId')));
  pm.expect(j.rol).eq('Cliente');
  pm.expect(j.correo).eq('cliente.test@connectaoficios.local');
  ['password','passwordHash','passwordResetToken','passwordResetTokenHash','passwordResetTokenExpiresAt'].forEach(k=>pm.expect(j).not.to.have.property(k));
});`;
  collection.item.push(folder('01 - API .NET - Angel Mate',[
    request('CON-566 Usuario publico existente','GET',dotnet+'/api/users/{{clienteId}}',200,'clienteToken',null,publicFields),
    request('CON-566 Sin autenticacion','GET',dotnet+'/api/users/{{clienteId}}',401),
    request('CON-567 Cuenta administrativa existente','GET',dotnet+'/api/admin/accounts/{{adminId}}',200,'adminprincipalToken',null,`
pm.test('CON-567 - administrador esperado sin secretos',()=>{
  const j=pm.response.json(); pm.expect(j.id).eq(Number(pm.collectionVariables.get('adminId')));
  pm.expect(j.rol).eq('Administrador'); pm.expect(j.rolId).eq(3);
  pm.expect(j.correo).eq('admin.test@connectaoficios.local');
  ['password','passwordHash','passwordResetTokenHash'].forEach(k=>pm.expect(j).not.to.have.property(k));
});`),
    request('CON-567 Cuenta administrativa sin token','GET',dotnet+'/api/admin/accounts/{{adminId}}',401),
    request('CON-567 Cliente sin permisos','GET',dotnet+'/api/admin/accounts/{{adminId}}',403,'clienteToken'),
    request('CON-569 API .NET disponible','GET',dotnet+'/health',200,null,null,`pm.test('CON-569 - health valido',()=>{const j=pm.response.json();pm.expect(j.status).eq('OK');pm.expect(j.api).eq('ConnectaOficios.Api');});`)
  ]));
  collection.item.push(folder('02 - API Java - Angel Mate',[
    request('CON-486 API Java disponible','GET',java+'/api/health',200,null,null,`pm.test('CON-486 - datasource disponible',()=>pm.expect(pm.response.json().status).eq('UP'));`),
    request('CON-485 Preparar categoria exclusiva','POST',java+'/api/categorias',201,'adminToken',{nombre:'QA Newman {{runId}}',descripcion:'Fixture exclusivo de API testing'},`pm.test('Categoria generada',()=>{const j=pm.response.json();pm.expect(j.id).to.be.a('number').above(0);pm.collectionVariables.set('categoriaId',j.id);});`),
    request('CON-485 Preparar zona exclusiva','POST',java+'/api/zonas-cobertura',201,'adminToken',{departamento:'QA Testing',municipio:'Newman {{runId}}',localidad:'Fixture exclusivo'},`pm.test('Zona generada',()=>{const j=pm.response.json();pm.expect(j.id).to.be.a('number').above(0);pm.collectionVariables.set('zonaId',j.id);});`),
    request('CON-485 Preparar plan exclusivo','POST',java+'/api/planes-promocion',201,'adminToken',{nombre:'QA Newman {{runId}}',descripcion:'Plan exclusivo sin pago real',duracionDias:1,precio:1},`pm.test('Plan valido generado',()=>{const j=pm.response.json();pm.expect(j.id).to.be.a('number').above(0);pm.expect(j.activo).eq(true);pm.collectionVariables.set('planPromocionId',j.id);});`),
    request('CON-485 Login .NET trabajador nuevo','POST',dotnet+'/api/users/login',200,null,{correo:'{{javaTrabajadorEmail}}',password:'{{password}}'},`
pm.test('JWT real .NET para fixtures Java',()=>{const j=pm.response.json();pm.expect(j.token).to.be.a('string').and.not.empty;pm.expect(j.usuario.id).eq(Number(pm.collectionVariables.get('javaTrabajadorId')));pm.expect(j.usuario.rol).eq('Trabajador');pm.collectionVariables.set('javaTrabajadorToken',j.token);});`),
    request('CON-485 Preparar perfil exclusivo','POST',java+'/api/perfiles-trabajador',201,'javaTrabajadorToken',{oficioPrincipal:'Testing de API',descripcionProfesional:'Fixture Newman {{runId}}',zonaPrincipalId:'{{zonaId}}'},`
pm.test('Perfil asociado a identidad .NET',()=>{const j=pm.response.json();pm.expect(j.id).to.be.a('number').above(0);pm.expect(j.trabajadorId).eq(Number(pm.collectionVariables.get('javaTrabajadorId')));pm.collectionVariables.set('perfilTrabajadorId',j.id);});`),
    request('CON-485 Preparar servicio exclusivo','POST',java+'/api/servicios',201,'javaTrabajadorToken',{perfilTrabajadorId:'{{perfilTrabajadorId}}',categoriaId:'{{categoriaId}}',titulo:'QA Newman {{runId}}',descripcion:'Servicio ficticio exclusivo de pruebas locales',tarifaMinima:1,tarifaMaxima:2,zonasCoberturaIds:['{{zonaId}}']},`
pm.test('Servicio propio activo',()=>{const j=pm.response.json();pm.expect(j.id).to.be.a('number').above(0);pm.expect(j.estado).eq('ACTIVO');pm.expect(j.perfilTrabajadorId).eq(Number(pm.collectionVariables.get('perfilTrabajadorId')));pm.collectionVariables.set('servicioId',j.id);});`),
    request('CON-485 Contratar promocion','POST',java+'/api/promociones',201,'javaTrabajadorToken',{servicioId:'{{servicioId}}',planId:'{{planPromocionId}}'},`
pm.test('CON-485 - promocion creada para servicio y trabajador propios',()=>{
  const j=pm.response.json();pm.expect(j.id).to.be.a('number').above(0);
  pm.expect(j.servicioId).eq(Number(pm.collectionVariables.get('servicioId')));
  pm.expect(j.planId).eq(Number(pm.collectionVariables.get('planPromocionId')));
  pm.expect(j.trabajadorId).eq(Number(pm.collectionVariables.get('javaTrabajadorId')));
  pm.expect(j.estado).eq('PENDIENTE');pm.expect(j.vigente).eq(false);
  pm.collectionVariables.set('promocionId',j.id);
});`),
    request('CON-485 Consultar promocion creada','GET',java+'/api/promociones/{{promocionId}}',200,'javaTrabajadorToken',null,`
pm.test('CON-485 - consulta conserva asociaciones y estado',()=>{
  const j=pm.response.json();pm.expect(j.id).eq(Number(pm.collectionVariables.get('promocionId')));
  pm.expect(j.servicioId).eq(Number(pm.collectionVariables.get('servicioId')));
  pm.expect(j.planId).eq(Number(pm.collectionVariables.get('planPromocionId')));
  pm.expect(j.trabajadorId).eq(Number(pm.collectionVariables.get('javaTrabajadorId')));
  pm.expect(j.estado).eq('PENDIENTE');pm.expect(j.planNombre).eq('QA Newman '+pm.collectionVariables.get('runId'));
  pm.expect(j.fechaCreacion).to.be.a('string').and.not.empty;
});`)
  ]));
  collection.item.push(folder('03 - Integracion .NET - Java - Angel Mate',[
    request('CON-487 JWT .NET aceptado por Java','GET',java+'/api/planes-promocion/activos?page=0&size=10&sort=id,desc',200,'authToken',null,`
pm.test('CON-487 - token de login .NET autoriza respuesta Java',()=>{pm.expect(pm.collectionVariables.get('authToken')).eq(pm.collectionVariables.get('clienteToken'));const j=pm.response.json();pm.expect(j.content).to.be.an('array').and.not.empty;pm.expect(j.content.some(p=>p.id===Number(pm.collectionVariables.get('planPromocionId')))).eq(true);});`),
    request('CON-487 Sin JWT Java rechaza acceso','GET',java+'/api/planes-promocion/activos?page=0&size=10&sort=id,desc',401),
    request('CON-488 Rol trabajador del JWT reconocido','GET',java+'/api/promociones/mis-promociones',200,'javaTrabajadorToken',null,`
pm.test('CON-488 - rol .NET trabajador y recurso esperado',()=>{const token=pm.collectionVariables.get('javaTrabajadorToken');const claims=JSON.parse(atob(token.split('.')[1].replace(/-/g,'+').replace(/_/g,'/')));pm.expect(claims['http://schemas.microsoft.com/ws/2008/06/identity/claims/role']).eq('Trabajador');const j=pm.response.json();pm.expect(j).to.be.an('array').and.not.empty;pm.expect(j.some(p=>p.id===Number(pm.collectionVariables.get('promocionId')))).eq(true);j.forEach(p=>pm.expect(p.trabajadorId).eq(Number(pm.collectionVariables.get('javaTrabajadorId'))));});`),
    request('CON-488 Rol cliente no obtiene recurso trabajador','GET',java+'/api/promociones/mis-promociones',403,'clienteToken'),
    request('CON-489 Administrador accede a promociones','GET',java+'/api/promociones?page=0&size=10&sort=id,desc',200,'adminToken',null,`
pm.test('CON-489 - acceso administrativo autorizado',()=>{const j=pm.response.json();pm.expect(j.content).to.be.an('array').and.not.empty;pm.expect(j.content.some(p=>p.id===Number(pm.collectionVariables.get('promocionId')))).eq(true);});`),
    request('CON-489 Cliente no obtiene recurso administrativo','GET',java+'/api/promociones?page=0&size=10&sort=id,desc',403,'clienteToken'),
    request('CON-490 JWT manipulado rechazado','GET',java+'/api/planes-promocion/activos?page=0&size=10&sort=id,desc',401,'angelInvalidToken',null,`pm.test('CON-490 - desafio Bearer por firma invalida',()=>pm.expect(pm.response.headers.get('WWW-Authenticate')).to.match(/Bearer/i));`,
`const token=pm.collectionVariables.get('authToken');if(!token||token.split('.').length!==3)throw new Error('CON-490 requires a successful real .NET login');const parts=token.split('.');parts[2]=(parts[2][0]==='A'?'B':'A')+parts[2].slice(1);pm.variables.set('angelInvalidToken',parts.join('.'));`),
    request('CON-491 Consultar identidad origen .NET','GET',dotnet+'/api/users/{{javaTrabajadorId}}',200,'javaTrabajadorToken',null,`
pm.test('CON-491 - usuario origen existe en .NET',()=>{const j=pm.response.json();pm.expect(j.id).eq(Number(pm.collectionVariables.get('javaTrabajadorId')));pm.expect(j.rol).eq('Trabajador');pm.expect(j.correo).eq(pm.collectionVariables.get('javaTrabajadorEmail'));pm.collectionVariables.set('relatedDotNetId',j.id);});`),
    request('CON-491 Relacion usuario .NET y perfil Java','GET',java+'/api/perfiles-trabajador/trabajador/{{javaTrabajadorId}}',200,'javaTrabajadorToken',null,`
pm.test('CON-491 - identidad consistente entre backends y promocion',()=>{const j=pm.response.json();pm.expect(j.id).eq(Number(pm.collectionVariables.get('perfilTrabajadorId')));pm.expect(j.trabajadorId).eq(Number(pm.collectionVariables.get('relatedDotNetId')));pm.expect(j.trabajadorId).eq(Number(pm.collectionVariables.get('javaTrabajadorId')));pm.expect(j.oficioPrincipal).eq('Testing de API');});`)
  ]));
};
