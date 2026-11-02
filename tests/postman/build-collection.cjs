// Rebuild the checked-in collection when endpoint contracts change.
const fs = require('fs');
const path = require('path');
const item = [];
function add(name, method, route, status, body, token, assertions = []) {
  item.push({name, request: {method, header: [{key:'Content-Type',value:'application/json'}, ...(token ? [{key:'Authorization',value:`Bearer {{${token}}}`}]:[])], url: '{{baseUrl}}'+route,
    ...(body ? {body:{mode:'raw',raw:JSON.stringify(body),options:{raw:{language:'json'}}}}:{})},
    event:[{listen:'test',script:{type:'text/javascript',exec:[`pm.test(${JSON.stringify(name)}, () => pm.response.to.have.status(${status}));`, ...assertions]}}]});
}
for (const [prefix,role] of [['cliente','Cliente'],['trabajador','Trabajador'],['admin','Administrador'],['adminprincipal','AdministradorPrincipal']]) {
  add('Login '+role,'POST','/api/users/login',200,{correo:prefix+'.test@connectaoficios.local',password:'{{password}}'},null,[
    `if (pm.response.code === 200) { const j = pm.response.json(); pm.collectionVariables.set('${prefix}Token', j.token); pm.collectionVariables.set('${prefix}Id', j.usuario.id); pm.test('Rol exacto', () => pm.expect(j.usuario.rol).eq('${role}')); }`
  ]);
}
add('Sin token','GET','/api/admin/users',401);
add('Token invalido','GET','/api/admin/users',401,null,'invalidToken');
add('Cliente no administra','GET','/api/admin/users',403,null,'clienteToken');
add('Trabajador no administra','GET','/api/admin/users',403,null,'trabajadorToken');
add('Admin no es Principal','GET','/api/admin/accounts/',403,null,'adminToken');
add('Cuenta autenticada','GET','/api/users/me',200,null,'clienteToken',[
  "pm.test('Identidad para avatar', () => { const j = pm.response.json(); ['id','nombre','correo','rol'].forEach(k => pm.expect(j).has.property(k)); pm.expect(j).not.has.property('fotoUrl'); });"
]);
for (const role of [1,2]) add('Registro rol '+role,'POST','/api/users/register',201,{nombre:'Newman local',correo:`newman-${role}-{{$guid}}@connectaoficios.local`,password:'{{password}}',rolId:role});
add('Registro invalido','POST','/api/users/register',400,{nombre:'',correo:'invalid',password:'x',rolId:1});
add('Registro no eleva privilegios','POST','/api/users/register',400,{nombre:'Local',correo:'local@connectaoficios.local',password:'{{password}}',rolId:4});
add('Correo duplicado','POST','/api/users/register',409,{nombre:'Local',correo:'cliente.test@connectaoficios.local',password:'{{password}}',rolId:1});
add('Password incorrecta','POST','/api/users/login',401,{correo:'cliente.test@connectaoficios.local',password:'Incorrecta!'});
for (const route of ['/api/admin/users?page=1&pageSize=10','/api/admin/users?page=2&pageSize=10','/api/admin/users?page=1&pageSize=10&nombre=Cliente&correo=connectaoficios.local&rolId=1&estado=1','/api/admin/accounts/?page=1&pageSize=10']) {
  add('Paginacion '+route,'GET',route,200,null,'adminprincipalToken',[
    "pm.test('Contrato paginado', () => { const j = pm.response.json(); pm.expect(j.pageSize).eq(10); pm.expect(j.items.length).at.most(10); ['page','totalItems','totalPages'].forEach(k => pm.expect(j).has.property(k)); j.items.forEach(u => ['id','nombre','correo','rol'].forEach(k => pm.expect(u).has.property(k))); });"
  ]);
}
add('Busqueda usuarios','GET','/api/admin/users/search?texto=local&limit=10',200,null,'adminToken');
add('Busqueda admins','GET','/api/admin/accounts/search?texto=local&limit=10',200,null,'adminprincipalToken');
add('Detalle usuario','GET','/api/admin/users/{{clienteId}}',200,null,'adminToken');
add('Actualizar cuenta','PUT','/api/users/me',200,{nombre:'Cliente local'},'clienteToken');
add('Crear administrador desechable','POST','/api/admin/accounts/',201,{nombre:'Newman Admin',correo:'newman-admin-{{$guid}}@connectaoficios.local',password:'{{password}}',rolId:3},'adminprincipalToken',[
  "if (pm.response.code === 201) pm.collectionVariables.set('disposableAdminId',pm.response.json().id);"
]);
add('Actualizar administrador','PUT','/api/admin/accounts/{{disposableAdminId}}',200,{nombre:'Newman actualizado'},'adminprincipalToken');
add('Desactivar administrador','PATCH','/api/admin/accounts/{{disposableAdminId}}/status',200,{estado:2},'adminprincipalToken');
add('Eliminar administrador desechable','DELETE','/api/admin/accounts/{{disposableAdminId}}',200,null,'adminprincipalToken');
// Runtime guard prevents even an environment override from sending requests to production.
const collection = {info:{name:'ConnectaOficios .NET local',schema:'https://schema.getpostman.com/json/collection/v2.1.0/collection.json'},
  variable:[{key:'invalidToken',value:'invalid.token.signature'}],
  event:[{listen:'prerequest',script:{type:'text/javascript',exec:["if (!new RegExp('^http://(localhost|127[.]0[.]0[.]1|connectaoficios-api)(:[0-9]+)?$').test(pm.environment.get('baseUrl'))) { pm.execution.skipRequest(); throw new Error('Solo API local'); }"]}}],item};
// Add Angel's cases without changing any of the original requests or assertions.
require('./extend-angel.cjs')(collection);
fs.writeFileSync(path.join(__dirname,'local.collection.json'),JSON.stringify(collection,null,2)+'\n');
const values = [
  {key:'baseUrl',value:'http://127.0.0.1:8080',enabled:true},
  {key:'baseUrlDotNet',value:'http://127.0.0.1:8080',enabled:true},
  {key:'baseUrlJava',value:'http://127.0.0.1:8081',enabled:true},
  {key:'password',value:'',enabled:true,type:'secret'}
];
fs.writeFileSync(path.join(__dirname,'local.environment.json'),JSON.stringify({name:'ConnectaOficios - testing local (.NET SQL Express / Java H2 MySQL mode)',values},null,2)+'\n');
