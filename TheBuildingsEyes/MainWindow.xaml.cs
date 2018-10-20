using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using Microsoft.Azure.NotificationHubs;
using OpenCvSharp;
using OpenCvSharp.Extensions;
using VideoFrameAnalyzer;
using Common = Microsoft.ProjectOxford.Common;
using FaceAPI = Microsoft.ProjectOxford.Face;
using VisionAPI = Microsoft.ProjectOxford.Vision;

namespace TheBuildingsEyes
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : System.Windows.Window
    {
        const string faceApiKey = "490d29b25fd44c74a2f9f69f1c5bbde7";
        const string faceApiHost = "https://eastasia.api.cognitive.microsoft.com/face/v1.0";
        const string visionApiKey = "ffc78c4cceeb41cd9bcedeecdfe9c231";
        const string visionApiHost = "https://eastasia.api.cognitive.microsoft.com/vision/v1.0";
        const int measurementInterval = 3; // seconds
        private Guid saschaPersonId = Guid.Parse("347e4d82-0e71-4a81-875f-0df089eda70f");
        NotificationHubClient notificationClient = NotificationHubClient
                .CreateClientFromConnectionString("Endpoint=sb://smartone.servicebus.windows.net/;SharedAccessKeyName=DefaultFullSharedAccessSignature;SharedAccessKey=0tcXJaemth4yv+hO/AMgI9S2H0GVyJoo7lY2DH5k6C4=", "SmarToneNotifications", true);

        private FaceAPI.FaceServiceClient _faceClient = null;
        private VisionAPI.VisionServiceClient _visionClient = null;
        private readonly FrameGrabber<LiveCameraResult> _grabber = null;
        private static readonly ImageEncodingParam[] s_jpegParams = {
            new ImageEncodingParam(ImwriteFlags.JpegQuality, 60)
        };
        private readonly CascadeClassifier _localFaceDetector = new CascadeClassifier();
        private LiveCameraResult _latestResultsToDisplay = null;
        private DateTime _startTime;

        public MainWindow()
        {
            InitializeComponent();

            // Create grabber. 
            _grabber = new FrameGrabber<LiveCameraResult>();

            // Set up a listener for when the client receives a new frame.
            _grabber.NewFrameProvided += NewFrameHandler;

            // Set up a listener for when the client receives a new result from an API call. 
            _grabber.NewResultAvailable += NewResultHandler;

            // Create local face detector. 
            _localFaceDetector.Load("Data/haarcascade_frontalface_alt2.xml");

            StopButton.Visibility = Visibility.Collapsed;
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            IconHelper.RemoveIcon(this);
        }

        private void NewFrameHandler(object s, FrameGrabber<LiveCameraResult>.NewFrameEventArgs e)
        {
            // Local face detection. 
            var rects = _localFaceDetector.DetectMultiScale(e.Frame.Image);
            // Attach faces to frame. 
            e.Frame.UserData = rects;

            // The callback may occur on a different thread, so we must use the
            // MainWindow.Dispatcher when manipulating the UI. 
            Dispatcher.BeginInvoke((Action)(() =>
               {
                   // If we're fusing client-side face detection with remote analysis, show the
                   // new frame now with the most recent analysis available. 
                   RightImage.Source = VisualizeResult(e.Frame);
               }));
        }

        private void NewResultHandler(object s, FrameGrabber<LiveCameraResult>.NewResultEventArgs e)
        {
            Dispatcher.BeginInvoke((Action)(() =>
            {
                if (e.TimedOut)
                {
                    //MessageArea.Text = "API call timed out.";
                }
                else if (e.Exception != null)
                {
                    string apiName = "";
                    string message = e.Exception.Message;
                    var faceEx = e.Exception as FaceAPI.FaceAPIException;
                    var emotionEx = e.Exception as Common.ClientException;
                    var visionEx = e.Exception as VisionAPI.ClientException;
                    if (faceEx != null)
                    {
                        apiName = "Face";
                        message = faceEx.ErrorMessage;
                    }
                    else if (emotionEx != null)
                    {
                        apiName = "Emotion";
                        message = emotionEx.Error.Message;
                    }
                    else if (visionEx != null)
                    {
                        apiName = "Computer Vision";
                        message = visionEx.Error.Message;
                    }
                    //MessageArea.Text = string.Format("{0} API call failed on frame {1}. Exception: {2}", apiName, e.Frame.Metadata.Index, message);
                }
                else
                {
                    _latestResultsToDisplay = e.Analysis;

                       // Display the image and visualization in the right pane. 
                       if (true)
                    {
                        RightImage.Source = VisualizeResult(e.Frame);
                    }
                }
            }));
        }

        private async Task<LiveCameraResult> AnalysisFunction(VideoFrame frame)
        {
            // Reset data
            await Dispatcher.BeginInvoke((Action)(() =>
             {

             }));

            // Encode image. 
            var jpg = frame.Image.ToMemoryStream(".jpg", s_jpegParams);

            var faces = await _faceClient.DetectAsync(jpg);
            var faceIds = faces.Select(face => face.FaceId).ToArray();

            // Submit image to API. 
            var results = await _faceClient.IdentifyAsync("residents", faceIds);
            Color? colorToUse = null;

            foreach (var identifyResult in results)
            {
                Console.WriteLine("Result of face: {0}", identifyResult.FaceId);
                if (identifyResult.Candidates.Length == 0)
                {
                    Console.WriteLine("No one identified");
                    await Dispatcher.BeginInvoke((Action)(() =>
                     {
                         VisitorImage.Visibility = Visibility.Visible;
                     }));
                    try
                    {
                        await notificationClient.SendAppleNativeNotificationAsync("{ \"elevator\": true, \"aircon\": false }");
                    }
                    catch(Exception ex)
                    {
                        // Ignore
                    }
                }
                else
                {
                    // Get top 1 among all candidates returned
                    var candidateId = identifyResult.Candidates[0].PersonId;
                    var person = await _faceClient.GetPersonAsync("residents", candidateId);
                    Console.WriteLine("Identified as {0}", person.Name);
                    if(person.PersonId == saschaPersonId)
                    {
                        colorToUse = new Color { R = 0, G = 255, B = 0, A = 255 };
                        await Dispatcher.BeginInvoke((Action)(() =>
                         {
                             ResidentImage.Visibility = Visibility.Visible;
                             PackageImage.Visibility = Visibility.Visible;
                         }));
                        try
                        {
                            await notificationClient.SendAppleNativeNotificationAsync("{\"aps\": { \"content-available\": 1, \"elevator\": true, \"aircon\": true }}");
                        }
                        catch(Exception ex)
                        {
                            // Ignore
                        }
                    }
                }
            }

            return new LiveCameraResult { Faces = faces, Color = colorToUse };
        }

        private BitmapSource VisualizeResult(VideoFrame frame)
        {
            // Draw any results on top of the image. 
            BitmapSource visImage = frame.Image.ToBitmapSource();

            var result = _latestResultsToDisplay;
            if (result != null)
            {
                // See if we have local face detections for this image.
                var clientFaces = (OpenCvSharp.Rect[])frame.UserData;
                if (clientFaces != null && result.Faces != null)
                {
                    // If so, then the analysis results might be from an older frame. We need to match
                    // the client-side face detections (computed on this frame) with the analysis
                    // results (computed on the older frame) that we want to display. 
                    MatchAndReplaceFaceRectangles(result.Faces, clientFaces);
                }

                visImage = Visualization.DrawFaces(visImage, result.Faces, result.Name, result.Color);
            }

            return visImage;
        }

        private async void StartButton_Click(object sender, RoutedEventArgs e)
        {
            StopButton.Visibility = Visibility.Visible;
            StartButton.Visibility = Visibility.Collapsed;

            // Create API clients. 
            _faceClient = new FaceAPI.FaceServiceClient(faceApiKey, faceApiHost);
            _visionClient = new VisionAPI.VisionServiceClient(visionApiKey, visionApiHost);

            // How often to analyze. 
            _grabber.TriggerAnalysisOnInterval(TimeSpan.FromSeconds(measurementInterval));

            // What to do for analysis
            _grabber.AnalysisFunction = AnalysisFunction;

            // Reset message. 
            //MessageArea.Text = "";

            // Record start time, for auto-stop
            _startTime = DateTime.Now;

            await _grabber.StartProcessingCameraAsync(0);
        }

        private async void StopButton_Click(object sender, RoutedEventArgs e)
        {
            await _grabber.StopProcessingAsync();
            StopButton.Visibility = Visibility.Collapsed;
            StartButton.Visibility = Visibility.Visible;
        }

        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri));
            e.Handled = true;
        }

        private FaceAPI.Contract.Face CreateFace(FaceAPI.Contract.FaceRectangle rect)
        {
            return new FaceAPI.Contract.Face
            {
                FaceRectangle = new FaceAPI.Contract.FaceRectangle
                {
                    Left = rect.Left,
                    Top = rect.Top,
                    Width = rect.Width,
                    Height = rect.Height
                }
            };
        }

        private FaceAPI.Contract.Face CreateFace(VisionAPI.Contract.FaceRectangle rect)
        {
            return new FaceAPI.Contract.Face
            {
                FaceRectangle = new FaceAPI.Contract.FaceRectangle
                {
                    Left = rect.Left,
                    Top = rect.Top,
                    Width = rect.Width,
                    Height = rect.Height
                }
            };
        }

        private FaceAPI.Contract.Face CreateFace(Common.Rectangle rect)
        {
            return new FaceAPI.Contract.Face
            {
                FaceRectangle = new FaceAPI.Contract.FaceRectangle
                {
                    Left = rect.Left,
                    Top = rect.Top,
                    Width = rect.Width,
                    Height = rect.Height
                }
            };
        }

        private void MatchAndReplaceFaceRectangles(FaceAPI.Contract.Face[] faces, OpenCvSharp.Rect[] clientRects)
        {
            // Use a simple heuristic for matching the client-side faces to the faces in the
            // results. Just sort both lists left-to-right, and assume a 1:1 correspondence. 

            // Sort the faces left-to-right. 
            var sortedResultFaces = faces
                .OrderBy(f => f.FaceRectangle.Left + 0.5 * f.FaceRectangle.Width)
                .ToArray();

            // Sort the clientRects left-to-right.
            var sortedClientRects = clientRects
                .OrderBy(r => r.Left + 0.5 * r.Width)
                .ToArray();

            // Assume that the sorted lists now corrrespond directly. We can simply update the
            // FaceRectangles in sortedResultFaces, because they refer to the same underlying
            // objects as the input "faces" array. 
            for (int i = 0; i < Math.Min(faces.Length, clientRects.Length); i++)
            {
                // convert from OpenCvSharp rectangles
                OpenCvSharp.Rect r = sortedClientRects[i];
                sortedResultFaces[i].FaceRectangle = new FaceAPI.Contract.FaceRectangle { Left = r.Left, Top = r.Top, Width = r.Width, Height = r.Height };
            }
        }
    }
}
