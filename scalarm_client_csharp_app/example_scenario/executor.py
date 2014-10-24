#!/usr/bin/env python2

import os, subprocess, sys, tarfile, json

def get_json(file_name):
    with open(file_name, 'rb+') as input_file:
        h = json.JSONDecoder().decode(input_file.read())
    return h

def make_tarfile(file_name, output_filename):
    from contextlib import closing
    with closing(tarfile.open(output_filename, "w:gz")) as tar:
        tar.add(file_name)

this_cd = os.path.dirname(os.path.realpath(sys.argv[0]))

param1 = get_json('input.json')['parameter1']
param2 = get_json('input.json')['parameter2']

out = None
try:
    out = subprocess.Popen(['python2', "%s/bin/app.py" % this_cd, param1, param2], stdout=subprocess.PIPE).communicate()[0]
except subprocess.CalledProcessError as e:
    out = e.output

with open('output.txt', 'wb+') as output_file:
    output_file.write(out)

with open('output.json', 'wb+') as output_file:
    output_file.write(json.JSONEncoder().encode({'status': 'ok', 'results': {'product': float(out)}}))

make_tarfile('output.txt', 'output.tar.gz')
