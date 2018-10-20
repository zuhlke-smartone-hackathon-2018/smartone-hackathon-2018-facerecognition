using System.Windows.Media;

namespace TheBuildingsEyes
{
    // Class to hold all possible result types. 
    public class LiveCameraResult
    {
        public Microsoft.ProjectOxford.Face.Contract.Face[] Faces { get; set; } = null;
        public string Name { get; set; } = null;
        public Color? Color { get; set; } = null;
    }
}
