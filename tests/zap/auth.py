"""Authenticate ZAP as a local principal; never persist the bearer token."""
import json
import os
import urllib.request

def zap_started(zap, target):
    request = urllib.request.Request(
        'http://connectaoficios-api:8080/api/users/login',
        json.dumps({'correo': 'adminprincipal.test@connectaoficios.local',
                    'password': os.environ['TEST_PASSWORD']}).encode(),
        {'Content-Type': 'application/json'})
    with urllib.request.urlopen(request, timeout=30) as response:
        token = json.load(response)['token']
    zap.replacer.add_rule('Local bearer', 'true', 'REQ_HEADER', 'false',
                         'Authorization', 'Bearer ' + token, initiators='')
