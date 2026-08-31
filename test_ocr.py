import http.client, uuid, json

filepath = r'C:\Users\PC\.gemini\antigravity\brain\699d061c-2be2-4b38-a233-ce477ebd1c7f\uploaded_media_1788137857011.png'
boundary = uuid.uuid4().hex
headers = {'Content-Type': 'multipart/form-data; boundary=' + boundary, 'apikey': 'helloworld'}

with open(filepath, 'rb') as f:
    file_bytes = f.read()

payload = b'--' + boundary.encode() + b'\r\nContent-Disposition: form-data; name="file"; filename="image.png"\r\nContent-Type: image/png\r\n\r\n' + file_bytes + b'\r\n--' + boundary.encode() + b'--\r\n'
headers['Content-Length'] = str(len(payload))

conn = http.client.HTTPSConnection('api.ocr.space')
conn.request('POST', '/parse/image', payload, headers)
res = conn.getresponse()
data = json.loads(res.read().decode())
print(data.get('ParsedResults', [{}])[0].get('ParsedText', data))
