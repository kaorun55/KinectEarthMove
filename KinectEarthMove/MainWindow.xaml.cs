using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Media.Media3D;
using System.Windows.Threading;
using Microsoft.Kinect;

namespace KinectEarthMove
{
    public partial class MainWindow : System.Windows.Window
    {
        private KinectSensor kinect;
        private DispatcherTimer timer = new DispatcherTimer();
        private EarthTransform earthTransform = new EarthTransform();

        public MainWindow()
        {
            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            try {
                // timer to rotate and update the earth
                timer.Tick += new EventHandler( timer_Tick );
                timer.Interval = new TimeSpan( 0, 0, 0, 0, 30 );
                timer.Start();

                // initialize kinect
                if ( KinectSensor.KinectSensors.Count == 0 ) {
                    throw new Exception( "Kinectが接続されていません。" );
                }

                kinect = KinectSensor.KinectSensors[0];
                if ( kinect.Status != KinectStatus.Connected ) {
                    throw new Exception( "Kinectが利用可能ではありません。電源など接続を確認してください。" );
                }

                // set event handler for kinect
                kinect.ColorFrameReady += new EventHandler<ColorImageFrameReadyEventArgs>( kinect_ColorFrameReady );
                kinect.SkeletonFrameReady += new EventHandler<SkeletonFrameReadyEventArgs>( nui_SkeletonFrameReady );

                kinect.ColorStream.Enable( ColorImageFormat.RgbResolution640x480Fps30 );

                // set transform smoothing
                kinect.SkeletonStream.Enable( new TransformSmoothParameters
                {
                    Smoothing = 0.75f,
                    Correction = 0.0f,
                    Prediction = 0.0f,
                    JitterRadius = 0.05f,
                    MaxDeviationRadius = 0.04f
                } );

                kinect.Start();
            }
            catch ( Exception ex ) {
                MessageBox.Show( ex.Message );
                Environment.Exit( 0 );
            }
        }

        void kinect_ColorFrameReady( object sender, ColorImageFrameReadyEventArgs e )
        {
            using ( var imageFrame = e.OpenColorImageFrame() ) {
                if ( imageFrame != null ) {
                    // 32-bit per pixel, RGBA image
                    byte[] image = new byte[imageFrame.PixelDataLength];
                    imageFrame.CopyPixelDataTo( image );
                    video.Source = BitmapSource.Create( imageFrame.Width, imageFrame.Height, 96, 96,
                        PixelFormats.Bgr32, null, image, imageFrame.Width * imageFrame.BytesPerPixel );
                }
            }
        }

        // Timer Callback to display update
        private void timer_Tick(object sender, EventArgs e)
        {
            // self rotate the earth
            earthTransform.SelfRotation += 1.0;
            // transform by Kinect
            myEarthGeometry.Transform = earthTransform.GetTransform3D();
        }

        private readonly double tfactor = 5.0;
        private void nui_SkeletonFrameReady(object sender, SkeletonFrameReadyEventArgs e)
        {
            using ( var skeletonFrame = e.OpenSkeletonFrame() ) {
                if ( skeletonFrame  != null ) {
                    Skeleton[] skeletons = new Skeleton[skeletonFrame.SkeletonArrayLength];
                    skeletonFrame.CopySkeletonDataTo( skeletons );
                    SkeletonPoint shoulderC = new SkeletonPoint();
                    SkeletonPoint handR = new SkeletonPoint();
                    SkeletonPoint handL = new SkeletonPoint();
                    SkeletonPoint shoulderR = new SkeletonPoint();
                    SkeletonPoint shoulderL = new SkeletonPoint();

                    // find positions of shoulders and hands
                    foreach ( Skeleton data in skeletons ) {
                        if ( SkeletonTrackingState.Tracked == data.TrackingState ) {
                            JointCollection j = data.Joints;
                            shoulderC = j[JointType.ShoulderCenter].Position;
                            handL = j[JointType.HandLeft].Position;
                            handR = j[JointType.HandRight].Position;
                            shoulderL = j[JointType.ShoulderLeft].Position;
                            shoulderR = j[JointType.ShoulderRight].Position;
                            break;
                        }
                    }

                    // translate
                    // Find the center of both hands
                    Vector3D pos = new Vector3D( (handR.X + handL.X) / 2.0, (handR.Y + handL.Y) / 2.0, (handR.Z + handL.Z) / 2.0 );
                    // move to the center of both hand
                    earthTransform.Translate = new Vector3D( pos.X*tfactor, pos.Y*tfactor, pos.Z );
                    // scale
                    // find the vector from left hand to right hand
                    Vector3D hand = new Vector3D( handR.X - handL.X, handR.Y - handL.Y, handR.Z - handL.Z );
                    // find the vector from left shoulder to right shoulder
                    Vector3D shoulder = new Vector3D( shoulderR.X - shoulderL.X, shoulderR.Y - shoulderL.Y, shoulderR.Z - shoulderL.Z );
                    // scale the earth from the difference of lengths(squared) of inter-shoulders and inter-hands
                    // if same length scale to 0.8. longer inter-hand , bigger scale
                    earthTransform.Scale= hand.LengthSquared - shoulder.LengthSquared + 0.8;
                    // rotataion
                    // get the angle and axis of inter-hands vector to rotate the earth
                    hand.Normalize();
                    earthTransform.Angle = Vector3D.AngleBetween( new Vector3D( 1, 0, 0 ), hand );
                    earthTransform.Axis = Vector3D.CrossProduct( new Vector3D( 1, 0, 0 ), hand );
                }
            }
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            kinect.Stop();
            kinect.Dispose();
            Environment.Exit(0);
        }


    }



    /// <summary>
    /// Trasnformation Helper Class
    /// </summary>
    public class EarthTransform
    {
        private ScaleTransform3D _scale;
        private RotateTransform3D _rotate;
        private RotateTransform3D _self;
        private double _selfAngle;
        private Vector3D _axis;
        private double _angle;
        private TranslateTransform3D _translate;

        public double Scale
        {
            get
            {
                return _scale.ScaleX;
            }
            set
            {
                _scale = new ScaleTransform3D(value, value, value);
            }
        }

        public double Angle
        {
            get
            {
                return _angle;
            }
            set
            {
                _angle = value;
                _rotate = new RotateTransform3D(new AxisAngleRotation3D(_axis, value));
            }
        }

        public Vector3D Axis
        {
            get
            {
                return _axis;
            }
            set
            {
                _axis = value;
                _rotate = new RotateTransform3D(new AxisAngleRotation3D(value, _angle ));
            }
        }
        public Vector3D Translate
        {
            get
            {
                return new Vector3D(_translate.OffsetX, _translate.OffsetY, _translate.OffsetZ);
            }
            set
            {
                _translate = new TranslateTransform3D(value);
            }
        }

        public double SelfRotation 
        {
            get
            {
                return _selfAngle;
            }
            set
            {
                _selfAngle = value;
                _self = new RotateTransform3D(new AxisAngleRotation3D(new Vector3D(0,1,0), value));
            }
        }

        public EarthTransform()
        {
            this.Scale = 1.0;
            this.Angle = 0.0;
            this.Axis = new Vector3D(0.0, 1.0, 0.0);
            this.Translate = new Vector3D(0.0, 0.0, 0.0);
            this.SelfRotation = 0.0;
        }

        public Transform3D GetTransform3D()
        {
            Transform3DGroup t3dg = new Transform3DGroup();
            t3dg.Children.Add(_scale);
            t3dg.Children.Add(_self);
            t3dg.Children.Add(_rotate);
            t3dg.Children.Add(_translate);
            return t3dg;
        }

    }
}
