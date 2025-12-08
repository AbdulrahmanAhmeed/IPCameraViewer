# Python Backend for License Plate Recognition

## Overview

This document provides a comprehensive guide to building a Python backend service for real-time license plate recognition from IP camera streams. The backend will receive video frames from a .NET MAUI mobile application, detect license plates, extract text, and return the results.

## Architecture

```
.NET MAUI App → HTTP/WebSocket → Python Backend (FastAPI)
                                        ↓
                                  Frame Processing
                                        ↓
                          ┌──────────────┴──────────────┐
                          ↓                             ↓
                    Plate Detection              Frame Buffering
                    (YOLOv8/YOLOv10)                   ↓
                          ↓                      Optimization
                    Crop Plate Region
                          ↓
                    OCR Recognition
                    (EasyOCR/PaddleOCR)
                          ↓
                    Text Post-processing
                          ↓
                    Return Results → .NET MAUI App
```

## Technology Stack

### Core Framework
- **FastAPI**: Modern, fast web framework with automatic API documentation
- **Uvicorn**: ASGI server for production deployment

### Computer Vision & ML
- **YOLOv8** (Ultralytics): State-of-the-art object detection for plate localization
- **EasyOCR**: Robust OCR engine supporting multiple languages
- **OpenCV (cv2)**: Image processing and manipulation
- **NumPy**: Numerical operations on image arrays

### Alternative Options
- **PaddleOCR**: Faster alternative to EasyOCR
- **Tesseract**: Traditional OCR engine (lower accuracy for plates)
- **YOLOv10**: Latest version with improved speed

## Project Structure

```
plate-recognition-backend/
├── app/
│   ├── __init__.py
│   ├── main.py                 # FastAPI application entry point
│   ├── config.py               # Configuration settings
│   ├── models/
│   │   ├── __init__.py
│   │   ├── plate_detector.py  # YOLO detection logic
│   │   ├── plate_ocr.py       # OCR recognition logic
│   │   └── processor.py       # Main processing pipeline
│   ├── api/
│   │   ├── __init__.py
│   │   ├── routes.py          # API endpoints
│   │   └── schemas.py         # Pydantic models
│   ├── utils/
│   │   ├── __init__.py
│   │   ├── image_utils.py     # Image processing utilities
│   │   └── text_utils.py      # Text post-processing
│   └── core/
│       ├── __init__.py
│       └── logging.py         # Logging configuration
├── models/
│   └── plate_detector.pt      # Trained YOLO model
├── requirements.txt
├── .env
├── Dockerfile
└── README.md
```

## Installation & Setup

### 1. System Requirements

- Python 3.8 or higher
- 4GB+ RAM (8GB recommended for production)
- CUDA-capable GPU (optional, for faster processing)

### 2. Install Dependencies

Create `requirements.txt`:

```txt
fastapi==0.104.1
uvicorn[standard]==0.24.0
python-multipart==0.0.6
pillow==10.1.0
opencv-python==4.8.1.78
numpy==1.24.3
ultralytics==8.0.220
easyocr==1.7.1
python-dotenv==1.0.0
pydantic==2.5.0
pydantic-settings==2.1.0
```

Install packages:

```bash
pip install -r requirements.txt
```

### 3. Environment Configuration

Create `.env` file:

```env
# Server Configuration
HOST=0.0.0.0
PORT=8000
WORKERS=1

# Model Configuration
YOLO_MODEL_PATH=models/plate_detector.pt
YOLO_CONFIDENCE=0.5
YOLO_IOU_THRESHOLD=0.45

# OCR Configuration
OCR_LANGUAGES=['en']
OCR_GPU=False
OCR_CONFIDENCE=0.3

# Processing Configuration
MAX_IMAGE_SIZE=1920
ENABLE_GPU=False

# Logging
LOG_LEVEL=INFO
```

## Core Implementation

### 1. Configuration Management (`app/config.py`)

```python
from pydantic_settings import BaseSettings
from typing import List

class Settings(BaseSettings):
    # Server
    host: str = "0.0.0.0"
    port: int = 8000
    workers: int = 1
    
    # Model paths
    yolo_model_path: str = "models/plate_detector.pt"
    yolo_confidence: float = 0.5
    yolo_iou_threshold: float = 0.45
    
    # OCR settings
    ocr_languages: List[str] = ['en']
    ocr_gpu: bool = False
    ocr_confidence: float = 0.3
    
    # Processing
    max_image_size: int = 1920
    enable_gpu: bool = False
    
    # Logging
    log_level: str = "INFO"
    
    class Config:
        env_file = ".env"

settings = Settings()
```

### 2. Plate Detection (`app/models/plate_detector.py`)

```python
from ultralytics import YOLO
import cv2
import numpy as np
from typing import List, Tuple, Optional
from app.config import settings

class PlateDetector:
    def __init__(self, model_path: str = None):
        self.model_path = model_path or settings.yolo_model_path
        self.confidence = settings.yolo_confidence
        self.iou_threshold = settings.yolo_iou_threshold
        self.model = None
        self._load_model()
    
    def _load_model(self):
        """Load YOLO model"""
        try:
            self.model = YOLO(self.model_path)
            print(f"Model loaded successfully from {self.model_path}")
        except Exception as e:
            print(f"Error loading model: {e}")
            # Fallback to pretrained model
            self.model = YOLO('yolov8n.pt')
    
    def detect_plates(self, image: np.ndarray) -> List[Tuple[int, int, int, int, float]]:
        """
        Detect license plates in image
        
        Args:
            image: Input image as numpy array
            
        Returns:
            List of tuples (x1, y1, x2, y2, confidence)
        """
        results = self.model.predict(
            image,
            conf=self.confidence,
            iou=self.iou_threshold,
            verbose=False
        )
        
        plates = []
        for result in results:
            boxes = result.boxes
            for box in boxes:
                x1, y1, x2, y2 = box.xyxy[0].cpu().numpy()
                confidence = float(box.conf[0].cpu().numpy())
                plates.append((int(x1), int(y1), int(x2), int(y2), confidence))
        
        return plates
    
    def crop_plate(self, image: np.ndarray, bbox: Tuple[int, int, int, int]) -> np.ndarray:
        """
        Crop plate region from image
        
        Args:
            image: Original image
            bbox: Bounding box (x1, y1, x2, y2)
            
        Returns:
            Cropped plate image
        """
        x1, y1, x2, y2 = bbox
        # Add small padding
        padding = 5
        x1 = max(0, x1 - padding)
        y1 = max(0, y1 - padding)
        x2 = min(image.shape[1], x2 + padding)
        y2 = min(image.shape[0], y2 + padding)
        
        return image[y1:y2, x1:x2]
```

### 3. OCR Recognition (`app/models/plate_ocr.py`)

```python
import easyocr
import cv2
import numpy as np
from typing import List, Tuple, Optional
from app.config import settings

class PlateOCR:
    def __init__(self):
        self.reader = easyocr.Reader(
            settings.ocr_languages,
            gpu=settings.ocr_gpu
        )
        self.min_confidence = settings.ocr_confidence
    
    def preprocess_plate(self, plate_image: np.ndarray) -> np.ndarray:
        """
        Preprocess plate image for better OCR results
        
        Args:
            plate_image: Cropped plate image
            
        Returns:
            Preprocessed image
        """
        # Convert to grayscale
        if len(plate_image.shape) == 3:
            gray = cv2.cvtColor(plate_image, cv2.COLOR_BGR2GRAY)
        else:
            gray = plate_image
        
        # Increase contrast
        clahe = cv2.createCLAHE(clipLimit=2.0, tileGridSize=(8, 8))
        enhanced = clahe.apply(gray)
        
        # Denoise
        denoised = cv2.fastNlMeansDenoising(enhanced)
        
        # Adaptive thresholding
        binary = cv2.adaptiveThreshold(
            denoised,
            255,
            cv2.ADAPTIVE_THRESH_GAUSSIAN_C,
            cv2.THRESH_BINARY,
            11,
            2
        )
        
        return binary
    
    def recognize(self, plate_image: np.ndarray) -> Optional[Tuple[str, float]]:
        """
        Recognize text from plate image
        
        Args:
            plate_image: Cropped plate image
            
        Returns:
            Tuple of (text, confidence) or None
        """
        # Preprocess image
        processed = self.preprocess_plate(plate_image)
        
        # Run OCR on both original and processed images
        results_original = self.reader.readtext(plate_image)
        results_processed = self.reader.readtext(processed)
        
        # Combine results
        all_results = results_original + results_processed
        
        if not all_results:
            return None
        
        # Sort by confidence
        all_results.sort(key=lambda x: x[2], reverse=True)
        
        # Get best result above threshold
        for bbox, text, confidence in all_results:
            if confidence >= self.min_confidence:
                # Clean text
                cleaned_text = self._clean_text(text)
                if cleaned_text:
                    return cleaned_text, confidence
        
        return None
    
    def _clean_text(self, text: str) -> str:
        """
        Clean and format recognized text
        
        Args:
            text: Raw OCR text
            
        Returns:
            Cleaned text
        """
        # Remove special characters, keep alphanumeric and spaces
        import re
        cleaned = re.sub(r'[^A-Z0-9\s]', '', text.upper())
        
        # Remove extra spaces
        cleaned = ' '.join(cleaned.split())
        
        return cleaned
```

### 4. Processing Pipeline (`app/models/processor.py`)

```python
import cv2
import numpy as np
from typing import Dict, List, Optional
from app.models.plate_detector import PlateDetector
from app.models.plate_ocr import PlateOCR
import time

class PlateRecognitionProcessor:
    def __init__(self):
        self.detector = PlateDetector()
        self.ocr = PlateOCR()
    
    def process_frame(self, image: np.ndarray) -> Dict:
        """
        Process a single frame and return recognition results
        
        Args:
            image: Input image as numpy array
            
        Returns:
            Dictionary with results
        """
        start_time = time.time()
        
        # Detect plates
        plates = self.detector.detect_plates(image)
        
        results = []
        for i, (x1, y1, x2, y2, det_conf) in enumerate(plates):
            # Crop plate region
            plate_img = self.detector.crop_plate(image, (x1, y1, x2, y2))
            
            # Recognize text
            ocr_result = self.ocr.recognize(plate_img)
            
            if ocr_result:
                text, ocr_conf = ocr_result
                results.append({
                    'plate_number': text,
                    'confidence': float(ocr_conf),
                    'detection_confidence': float(det_conf),
                    'bbox': {
                        'x1': int(x1),
                        'y1': int(y1),
                        'x2': int(x2),
                        'y2': int(y2)
                    }
                })
        
        processing_time = time.time() - start_time
        
        return {
            'success': True,
            'plates_detected': len(plates),
            'plates_recognized': len(results),
            'results': results,
            'processing_time_ms': round(processing_time * 1000, 2)
        }
```

### 5. API Schemas (`app/api/schemas.py`)

```python
from pydantic import BaseModel
from typing import List, Optional

class BoundingBox(BaseModel):
    x1: int
    y1: int
    x2: int
    y2: int

class PlateResult(BaseModel):
    plate_number: str
    confidence: float
    detection_confidence: float
    bbox: BoundingBox

class RecognitionResponse(BaseModel):
    success: bool
    plates_detected: int
    plates_recognized: int
    results: List[PlateResult]
    processing_time_ms: float
    message: Optional[str] = None

class HealthResponse(BaseModel):
    status: str
    version: str
    models_loaded: bool
```

### 6. API Routes (`app/api/routes.py`)

```python
from fastapi import APIRouter, File, UploadFile, HTTPException
from fastapi.responses import JSONResponse
import cv2
import numpy as np
from app.models.processor import PlateRecognitionProcessor
from app.api.schemas import RecognitionResponse, HealthResponse
import io

router = APIRouter()
processor = PlateRecognitionProcessor()

@router.get("/health", response_model=HealthResponse)
async def health_check():
    """Health check endpoint"""
    return {
        "status": "healthy",
        "version": "1.0.0",
        "models_loaded": processor.detector.model is not None
    }

@router.post("/recognize", response_model=RecognitionResponse)
async def recognize_plate(file: UploadFile = File(...)):
    """
    Recognize license plate from uploaded image
    
    Args:
        file: Image file (JPEG, PNG)
        
    Returns:
        Recognition results with plate numbers and bounding boxes
    """
    try:
        # Read image file
        contents = await file.read()
        nparr = np.frombuffer(contents, np.uint8)
        image = cv2.imdecode(nparr, cv2.IMREAD_COLOR)
        
        if image is None:
            raise HTTPException(status_code=400, detail="Invalid image file")
        
        # Process frame
        result = processor.process_frame(image)
        
        return result
        
    except Exception as e:
        raise HTTPException(status_code=500, detail=f"Processing error: {str(e)}")

@router.post("/recognize/base64")
async def recognize_plate_base64(data: dict):
    """
    Recognize license plate from base64 encoded image
    
    Args:
        data: Dictionary with 'image' key containing base64 string
        
    Returns:
        Recognition results
    """
    try:
        import base64
        
        # Decode base64
        image_data = base64.b64decode(data['image'])
        nparr = np.frombuffer(image_data, np.uint8)
        image = cv2.imdecode(nparr, cv2.IMREAD_COLOR)
        
        if image is None:
            raise HTTPException(status_code=400, detail="Invalid image data")
        
        # Process frame
        result = processor.process_frame(image)
        
        return result
        
    except KeyError:
        raise HTTPException(status_code=400, detail="Missing 'image' key in request")
    except Exception as e:
        raise HTTPException(status_code=500, detail=f"Processing error: {str(e)}")
```

### 7. Main Application (`app/main.py`)

```python
from fastapi import FastAPI
from fastapi.middleware.cors import CORSMiddleware
from app.api.routes import router
from app.config import settings
import logging

# Configure logging
logging.basicConfig(
    level=settings.log_level,
    format='%(asctime)s - %(name)s - %(levelname)s - %(message)s'
)

logger = logging.getLogger(__name__)

# Create FastAPI app
app = FastAPI(
    title="License Plate Recognition API",
    description="Real-time license plate detection and recognition service",
    version="1.0.0"
)

# Configure CORS
app.add_middleware(
    CORSMiddleware,
    allow_origins=["*"],  # Configure appropriately for production
    allow_credentials=True,
    allow_methods=["*"],
    allow_headers=["*"],
)

# Include routers
app.include_router(router, prefix="/api/v1", tags=["recognition"])

@app.on_event("startup")
async def startup_event():
    logger.info("Starting License Plate Recognition API...")
    logger.info(f"Server running on {settings.host}:{settings.port}")

@app.on_event("shutdown")
async def shutdown_event():
    logger.info("Shutting down License Plate Recognition API...")

@app.get("/")
async def root():
    return {
        "message": "License Plate Recognition API",
        "version": "1.0.0",
        "docs": "/docs"
    }

if __name__ == "__main__":
    import uvicorn
    uvicorn.run(
        "app.main:app",
        host=settings.host,
        port=settings.port,
        reload=True
    )
```

## Training Custom YOLO Model

### 1. Dataset Preparation

You'll need a dataset with license plate annotations:

- **Public Datasets**: 
  - CCPD (Chinese City Parking Dataset)
  - OpenALPR Dataset
  - License Plate Dataset on Roboflow

- **Annotation Format**: YOLO format
  ```
  <class_id> <x_center> <y_center> <width> <height>
  ```

### 2. Dataset Structure

```
dataset/
├── images/
│   ├── train/
│   ├── val/
│   └── test/
└── labels/
    ├── train/
    ├── val/
    └── test/
```

### 3. Training Script

```python
from ultralytics import YOLO

# Load pretrained model
model = YOLO('yolov8n.pt')

# Train the model
results = model.train(
    data='plate_dataset.yaml',
    epochs=100,
    imgsz=640,
    batch=16,
    name='plate_detector',
    patience=20,
    save=True,
    plots=True
)

# Validate
metrics = model.val()

# Export
model.export(format='onnx')
```

### 4. Dataset Configuration (`plate_dataset.yaml`)

```yaml
path: ./dataset
train: images/train
val: images/val
test: images/test

nc: 1  # number of classes
names: ['license_plate']
```

## Running the Server

### Development Mode

```bash
# Using uvicorn directly
uvicorn app.main:app --reload --host 0.0.0.0 --port 8000

# Or using python
python -m app.main
```

### Production Mode

```bash
# Using uvicorn with workers
uvicorn app.main:app --host 0.0.0.0 --port 8000 --workers 4

# Or using gunicorn
gunicorn app.main:app -w 4 -k uvicorn.workers.UvicornWorker --bind 0.0.0.0:8000
```

## API Usage Examples

### 1. Health Check

```bash
curl http://localhost:8000/api/v1/health
```

Response:
```json
{
  "status": "healthy",
  "version": "1.0.0",
  "models_loaded": true
}
```

### 2. Recognize Plate (File Upload)

```bash
curl -X POST \
  http://localhost:8000/api/v1/recognize \
  -F "file=@plate_image.jpg"
```

Response:
```json
{
  "success": true,
  "plates_detected": 1,
  "plates_recognized": 1,
  "results": [
    {
      "plate_number": "ABC 123",
      "confidence": 0.92,
      "detection_confidence": 0.87,
      "bbox": {
        "x1": 450,
        "y1": 320,
        "x2": 650,
        "y2": 420
      }
    }
  ],
  "processing_time_ms": 245.67
}
```

### 3. Recognize Plate (Base64)

```bash
curl -X POST \
  http://localhost:8000/api/v1/recognize/base64 \
  -H "Content-Type: application/json" \
  -d '{"image": "BASE64_ENCODED_STRING"}'
```

## Docker Deployment

### Dockerfile

```dockerfile
FROM python:3.10-slim

# Install system dependencies
RUN apt-get update && apt-get install -y \
    libgl1-mesa-glx \
    libglib2.0-0 \
    libsm6 \
    libxext6 \
    libxrender-dev \
    libgomp1 \
    && rm -rf /var/lib/apt/lists/*

# Set working directory
WORKDIR /app

# Copy requirements
COPY requirements.txt .

# Install Python dependencies
RUN pip install --no-cache-dir -r requirements.txt

# Copy application code
COPY . .

# Expose port
EXPOSE 8000

# Run application
CMD ["uvicorn", "app.main:app", "--host", "0.0.0.0", "--port", "8000"]
```

### Build and Run

```bash
# Build image
docker build -t plate-recognition-api .

# Run container
docker run -p 8000:8000 plate-recognition-api
```

### Docker Compose

```yaml
version: '3.8'

services:
  api:
    build: .
    ports:
      - "8000:8000"
    environment:
      - LOG_LEVEL=INFO
      - ENABLE_GPU=false
    volumes:
      - ./models:/app/models
    restart: unless-stopped
```

## Performance Optimization

### 1. Image Preprocessing

- Resize images before processing (640x640 is optimal for YOLO)
- Convert to RGB/BGR consistently
- Normalize pixel values

### 2. Model Optimization

- Use quantized models (INT8) for faster inference
- Export to ONNX format for better compatibility
- Use smaller YOLO variants (YOLOv8n instead of YOLOv8x) if speed is critical

### 3. Batch Processing

```python
def process_batch(images: List[np.ndarray]) -> List[Dict]:
    """Process multiple frames in batch"""
    # Detection in batch
    all_plates = self.detector.model.predict(images, batch=len(images))
    
    results = []
    for img, plates in zip(images, all_plates):
        # Process each image's detections
        result = self._process_single_detection(img, plates)
        results.append(result)
    
    return results
```

### 4. Caching

Implement Redis caching for frequently seen plates:

```python
import redis

cache = redis.Redis(host='localhost', port=6379, db=0)

def get_cached_result(image_hash: str):
    """Get cached recognition result"""
    cached = cache.get(f"plate:{image_hash}")
    if cached:
        return json.loads(cached)
    return None

def cache_result(image_hash: str, result: dict, ttl: int = 300):
    """Cache recognition result"""
    cache.setex(f"plate:{image_hash}", ttl, json.dumps(result))
```

## Testing

### Unit Tests

```python
import pytest
from app.models.processor import PlateRecognitionProcessor
import cv2

def test_plate_detection():
    processor = PlateRecognitionProcessor()
    image = cv2.imread('test_images/plate1.jpg')
    result = processor.process_frame(image)
    
    assert result['success'] == True
    assert result['plates_detected'] > 0

def test_ocr_recognition():
    processor = PlateRecognitionProcessor()
    plate_img = cv2.imread('test_images/cropped_plate.jpg')
    result = processor.ocr.recognize(plate_img)
    
    assert result is not None
    assert len(result[0]) > 0  # Text should not be empty
```

### Integration Tests

```python
from fastapi.testclient import TestClient
from app.main import app

client = TestClient(app)

def test_health_endpoint():
    response = client.get("/api/v1/health")
    assert response.status_code == 200
    assert response.json()["status"] == "healthy"

def test_recognize_endpoint():
    with open("test_images/plate1.jpg", "rb") as f:
        response = client.post(
            "/api/v1/recognize",
            files={"file": ("plate1.jpg", f, "image/jpeg")}
        )
    
    assert response.status_code == 200
    data = response.json()
    assert data["success"] == True
```

## Monitoring & Logging

### Structured Logging

```python
import structlog

logger = structlog.get_logger()

def process_with_logging(image: np.ndarray):
    logger.info("processing_started", image_shape=image.shape)
    
    try:
        result = processor.process_frame(image)
        logger.info(
            "processing_completed",
            plates_detected=result['plates_detected'],
            processing_time=result['processing_time_ms']
        )
        return result
    except Exception as e:
        logger.error("processing_failed", error=str(e))
        raise
```

### Performance Metrics

Track key metrics:
- Average processing time
- Detection accuracy
- OCR accuracy
- API response times
- Error rates

## Troubleshooting

### Common Issues

1. **Low Detection Accuracy**
   - Retrain model with more diverse dataset
   - Adjust confidence threshold
   - Improve image quality

2. **Slow Processing**
   - Enable GPU acceleration
   - Use smaller model
   - Reduce image resolution
   - Implement batch processing

3. **OCR Errors**
   - Improve plate image preprocessing
   - Try different OCR engines (PaddleOCR vs EasyOCR)
   - Train custom OCR model

4. **Memory Issues**
   - Implement image size limits
   - Add garbage collection
   - Use worker processes instead of threads

## Security Considerations

1. **API Authentication**: Implement JWT or API key authentication
2. **Rate Limiting**: Prevent abuse with rate limits
3. **Input Validation**: Validate image size and format
4. **HTTPS**: Use SSL/TLS in production
5. **CORS**: Configure appropriately for your client apps

## Next Steps

1. Train custom YOLO model on your specific license plate format
2. Fine-tune OCR for your region's plate characters
3. Implement WebSocket for real-time streaming
4. Add database for storing recognition history
5. Create admin dashboard for monitoring
6. Implement advanced post-processing (plate format validation)

## Resources

- [Ultralytics YOLOv8 Documentation](https://docs.ultralytics.com/)
- [EasyOCR Documentation](https://github.com/JaidedAI/EasyOCR)
- [FastAPI Documentation](https://fastapi.tiangolo.com/)
- [Roboflow License Plate Datasets](https://universe.roboflow.com/search?q=license%20plate)

---

**Version**: 1.0.0  
**Last Updated**: December 2024