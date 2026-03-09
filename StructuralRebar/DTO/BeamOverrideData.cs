using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace antiGGGravity.StructuralRebar.DTO
{
    /// <summary>
    /// Configures Top Additional (Hogging) bars for a specific support in a continuous beam.
    /// </summary>
    public class SupportOverride : INotifyPropertyChanged
    {
        public int SupportIndex { get; set; }
        public string SupportName { get; set; }

        private string _t2BarTypeName;
        public string T2_BarTypeName
        {
            get => _t2BarTypeName;
            set { _t2BarTypeName = value; OnPropertyChanged(); }
        }

        private int _t2Count;
        public int T2_Count
        {
            get => _t2Count;
            set { _t2Count = value; OnPropertyChanged(); }
        }

        private string _t3BarTypeName;
        public string T3_BarTypeName
        {
            get => _t3BarTypeName;
            set { _t3BarTypeName = value; OnPropertyChanged(); }
        }

        private int _t3Count;
        public int T3_Count
        {
            get => _t3Count;
            set { _t3Count = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }

    /// <summary>
    /// Configures Bottom Additional (Sagging) bars for a specific span in a continuous beam.
    /// </summary>
    public class SpanOverride : INotifyPropertyChanged
    {
        public int SpanIndex { get; set; }
        public string SpanName { get; set; }

        private string _b2BarTypeName;
        public string B2_BarTypeName
        {
            get => _b2BarTypeName;
            set { _b2BarTypeName = value; OnPropertyChanged(); }
        }

        private int _b2Count;
        public int B2_Count
        {
            get => _b2Count;
            set { _b2Count = value; OnPropertyChanged(); }
        }

        private string _b3BarTypeName;
        public string B3_BarTypeName
        {
            get => _b3BarTypeName;
            set { _b3BarTypeName = value; OnPropertyChanged(); }
        }

        private int _b3Count;
        public int B3_Count
        {
            get => _b3Count;
            set { _b3Count = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
