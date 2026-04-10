from fastapi import FastAPI, UploadFile, File
from fastapi.middleware.cors import CORSMiddleware
from transformers import CLIPProcessor, CLIPModel
from PIL import Image
import torch
import io

app = FastAPI()

# Enable CORS for local .NET backend testing
app.add_middleware(
    CORSMiddleware,
    allow_origins=["*"],
    allow_credentials=True,
    allow_methods=["*"],
    allow_headers=["*"],
)

model_id = "openai/clip-vit-base-patch32"
# Load the model directly directly to avoid network timeouts and delays during first use
print(f"Loading CLIP model '{model_id}'...")
try:
    model = CLIPModel.from_pretrained(model_id)
    processor = CLIPProcessor.from_pretrained(model_id)
    print("CLIP model loaded successfully.")
except Exception as e:
    print(f"Failed to load CLIP model: {e}")

@app.post("/embed")
async def get_embedding(file: UploadFile = File(...)):
    try:
        contents = await file.read()
        image = Image.open(io.BytesIO(contents)).convert("RGB")
        
        # Prepare inputs
        inputs = processor(images=image, return_tensors="pt")
        
        # Forward pass to get features
        with torch.no_grad():
            outputs = model.get_image_features(**inputs)
            
        # Extract features vector safely
        if isinstance(outputs, torch.Tensor):
            image_features = outputs
        else:
            # If it's a model output object, extract the proper tensor
            if hasattr(outputs, "image_embeds"):
                image_features = outputs.image_embeds
            elif hasattr(outputs, "pooler_output"):
                image_features = outputs.pooler_output
            else:
                raise ValueError("Unknown output format from CLIP model")

        # L2 Normalize the extracted vectors so we can use simple Cosine Similarity (Dot Product)
        image_features = image_features / image_features.norm(p=2, dim=-1, keepdim=True)
        vector = image_features.squeeze().tolist()
        
        return {"embedding": vector}
    except Exception as e:
        return {"error": str(e)}

@app.get("/")
def read_root():
    return {"message": "PizzaDeli AI Microservice is running!"}
