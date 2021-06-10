using System;
using System.Drawing;
using Capture.Interface;

namespace Capture.Hook.Common
{
    [Serializable]
    public class ImageElement: Element
    {
        /// <summary>
        /// The image file bytes
        /// </summary>
        public virtual byte[] Image { get; set; }

        private Bitmap bitmap;
        internal virtual Bitmap Bitmap 
        {
            get
            {
                if (bitmap != null || Image == null) return bitmap;
                
                bitmap = Image.ToBitmap();
                ownsBitmap = true;

                return bitmap;
            }
            set => bitmap = value;
        }

        /// <summary>
        /// This value is multiplied with the source color (e.g. White will result in same color as source image)
        /// </summary>
        /// <remarks>
        /// Defaults to <see cref="System.Drawing.Color.White"/>.
        /// </remarks>
        public virtual Color Tint { get; set; } = Color.White;
        
        /// <summary>
        /// The location of where to render this image element
        /// </summary>
        public virtual Point Location { get; set; }

        public float Angle { get; set; }

        public float Scale { get; set; } = 1.0f;

        public string Filename { get; set; }

        private bool ownsBitmap;

        public ImageElement() { }

        public ImageElement(string filename):
            this(new Bitmap(filename), true)
        {
            Filename = filename;
        }

        public ImageElement(Bitmap bitmapIn, bool ownsImage = false)
        {
            bitmap = bitmapIn;
            ownsBitmap = ownsImage;
            Scale = 1.0f;
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            if (!disposing) return;
            if (!ownsBitmap) return;
                
            SafeDispose(Bitmap);
            Bitmap = null;
        }
    }
}
