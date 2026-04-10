"""Script test nhanh Python AI Service - chạy bằng: AiService\venv\Scripts\python test_ai.py"""
import urllib.request
import json
import os
import io

# ---- 1. Kiểm tra service còn sống không ----
try:
    res = urllib.request.urlopen("http://localhost:8000/")
    print("[OK] AI Service đang chạy:", json.loads(res.read()))
except Exception as e:
    print("[FAIL] AI Service không phản hồi:", e)
    exit(1)

# ---- 2. Tạo ảnh PNG giả (1x1 pixel đỏ) để test /embed ----
# tiny valid PNG bytes
PNG_1x1 = (
    b'\x89PNG\r\n\x1a\n\x00\x00\x00\rIHDR\x00\x00\x00\x01'
    b'\x00\x00\x00\x01\x08\x02\x00\x00\x00\x90wS\xde\x00\x00'
    b'\x00\x0cIDATx\x9cc\xf8\x0f\x00\x00\x01\x01\x00\x05\x18'
    b'\xd8N\x00\x00\x00\x00IEND\xaeB`\x82'
)

boundary = b"----TestBoundary7890"
body = (
    b"--" + boundary + b"\r\n"
    b'Content-Disposition: form-data; name="file"; filename="test.png"\r\n'
    b"Content-Type: image/png\r\n\r\n"
    + PNG_1x1 + b"\r\n"
    b"--" + boundary + b"--\r\n"
)

req = urllib.request.Request(
    "http://localhost:8000/embed",
    data=body,
    headers={"Content-Type": f"multipart/form-data; boundary={boundary.decode()}"}
)

try:
    res = urllib.request.urlopen(req)
    data = json.loads(res.read())
    emb = data.get("embedding", [])
    err = data.get("error", None)
    if err:
        print(f"[FAIL] AI Service trả về lỗi: {err}")
    elif len(emb) > 0:
        print(f"[OK] Embedding thành công! Vector độ dài: {len(emb)}, 3 phần tử đầu: {emb[:3]}")
    else:
        print(f"[FAIL] Embedding rỗng. Response đầy đủ: {data}")
except Exception as e:
    print(f"[FAIL] Lỗi khi gọi /embed: {e}")
