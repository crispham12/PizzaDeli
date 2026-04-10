@echo off
cd AiService
if not exist venv (
    echo Creating Python virtual environment...
    python -m venv venv
)
call venv\Scripts\activate.bat

echo Installing required AI dependencies (FastAPI, PyTorch, Transformers)...
pip install fastapi uvicorn torch transformers pillow python-multipart pydantic

echo Starting AI Microservice on http://localhost:8000 ...
uvicorn main:app --reload --port 8000
pause
