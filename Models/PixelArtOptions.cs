using System.ComponentModel;

namespace PixelArtGenerator.Models
{
    public class PixelArtOptions : INotifyPropertyChanged
    {
        private int _pixelSize = 16;
        private int _colorCount = 32;
        private string _palette = "default";
        private string _algorithm = "basic";
        private bool _enableDithering = false;
        private double _edgeSmoothing = 0.5;
        private double _contrast = 1.0;
        private double _brightness = 1.0;
        private double _saturation = 1.0;
        private double _ditherStrength = 0.1;
        private bool _enableCartoonEffect = false;

        public event PropertyChangedEventHandler PropertyChanged;

        public int PixelSize
        {
            get => _pixelSize;
            set
            {
                if (_pixelSize != value)
                {
                    _pixelSize = value;
                    OnPropertyChanged(nameof(PixelSize));
                }
            }
        }

        public int ColorCount
        {
            get => _colorCount;
            set
            {
                if (_colorCount != value)
                {
                    _colorCount = value;
                    OnPropertyChanged(nameof(ColorCount));
                }
            }
        }

        public string Palette
        {
            get => _palette;
            set
            {
                if (_palette != value)
                {
                    _palette = value;
                    OnPropertyChanged(nameof(Palette));
                }
            }
        }

        public string Algorithm
        {
            get => _algorithm;
            set
            {
                if (_algorithm != value)
                {
                    _algorithm = value;
                    OnPropertyChanged(nameof(Algorithm));
                }
            }
        }

        public bool EnableDithering
        {
            get => _enableDithering;
            set
            {
                if (_enableDithering != value)
                {
                    _enableDithering = value;
                    OnPropertyChanged(nameof(EnableDithering));
                }
            }
        }

        public double EdgeSmoothing
        {
            get => _edgeSmoothing;
            set
            {
                if (_edgeSmoothing != value)
                {
                    _edgeSmoothing = value;
                    OnPropertyChanged(nameof(EdgeSmoothing));
                }
            }
        }

        public double Contrast
        {
            get => _contrast;
            set
            {
                if (_contrast != value)
                {
                    _contrast = value;
                    OnPropertyChanged(nameof(Contrast));
                }
            }
        }

        public double Brightness
        {
            get => _brightness;
            set
            {
                if (_brightness != value)
                {
                    _brightness = value;
                    OnPropertyChanged(nameof(Brightness));
                }
            }
        }

        public double Saturation
        {
            get => _saturation;
            set
            {
                if (_saturation != value)
                {
                    _saturation = value;
                    OnPropertyChanged(nameof(Saturation));
                }
            }
        }

        public double DitherStrength
        {
            get => _ditherStrength;
            set
            {
                if (_ditherStrength != value)
                {
                    _ditherStrength = value;
                    OnPropertyChanged(nameof(DitherStrength));
                }
            }
        }

        public bool EnableCartoonEffect
        {
            get => _enableCartoonEffect;
            set
            {
                if (_enableCartoonEffect != value)
                {
                    _enableCartoonEffect = value;
                    OnPropertyChanged(nameof(EnableCartoonEffect));
                }
            }
        }

        public PixelArtOptions Clone()
        {
            return new PixelArtOptions
            {
                PixelSize = this.PixelSize,
                ColorCount = this.ColorCount,
                Palette = this.Palette,
                Algorithm = this.Algorithm,
                EnableDithering = this.EnableDithering,
                EdgeSmoothing = this.EdgeSmoothing,
                Contrast = this.Contrast,
                Brightness = this.Brightness,
                Saturation = this.Saturation,
                DitherStrength = this.DitherStrength,
                EnableCartoonEffect = this.EnableCartoonEffect
            };
        }

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}