using System;

namespace IPCameraViewer.Models
{
    /// <summary>
    /// Represents a detected license plate with recognition details
    /// </summary>
    public class PlateDetection
    {
        /// <summary>
        /// The recognized license plate number
        /// </summary>
        public string PlateNumber { get; set; } = string.Empty;

        /// <summary>
        /// OCR confidence score (0.0 to 1.0)
        /// </summary>
        public double Confidence { get; set; }

        /// <summary>
        /// Timestamp when the plate was detected
        /// </summary>
        public DateTime DetectedAt { get; set; }

        /// <summary>
        /// Name of the camera that detected the plate
        /// </summary>
        public string CameraName { get; set; } = string.Empty;

        /// <summary>
        /// Optional thumbnail image of the detected plate region
        /// </summary>
        public byte[]? ImageData { get; set; }

        /// <summary>
        /// Formatted display string for UI
        /// </summary>
        public string DisplayText => $"{PlateNumber} ({Confidence:P0}) - {DetectedAt:HH:mm:ss}";
    }
}
