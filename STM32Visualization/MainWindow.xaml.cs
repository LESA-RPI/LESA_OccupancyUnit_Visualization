using System;
using System.IO.Ports;
using System.Windows;
using System.Windows.Controls;
using System.Text;
using System.Windows.Threading;
using System.Windows.Documents;
using System.Collections.Generic;
using static System.Reflection.Metadata.BlobBuilder;
using System.Windows.Media;
using System.Diagnostics;
using static System.Collections.Specialized.BitVector32;
using System.Windows.Shapes;
using System.Timers;
using System.Threading.Tasks;
using System.Threading;
using System.Reflection.Metadata;

public class DataPacket
{
    public MatrixData Matrix { get; set; } = new MatrixData();
    public FrameData Frame { get; set; } = new FrameData();
    public List<BlobsData> Blobs { get; set; } = new List<BlobsData>();

    public override string ToString()
    {
        StringBuilder sb = new StringBuilder();

        if (Matrix != null && Matrix.Values.Count > 0)
        {
            sb.AppendLine("Matrix Data: " + string.Join(", ", Matrix.Values));
        }

        if (Frame != null)
        {
            sb.AppendLine($"Frame Data: Numblobs={Frame.NumBlobs}, X={Frame.delta_X}, Y={Frame.delta_Y}");
        }

        if (Blobs != null && Blobs.Count > 0)
        {
            sb.AppendLine("Blobs Data:");
            for (int i = 0; i < Blobs.Count; i++)
            {
                var blob = Blobs[i];
                sb.Append($"Blob {i + 1}:");
                sb.Append($"  ID: {blob.ID}");
                sb.Append($"  X: {blob.X}");
                sb.Append($"  Y: {blob.Y}");
                sb.Append($"  Height: {blob.Height}");
                sb.Append($"  Speed: {blob.Speed}");
                sb.Append($"  Direction: {blob.Direction}");
            }
        }

        return sb.ToString();
    }
    public void Clear()
    {
        if (Matrix != null)
        {
            Matrix.Values.Clear();
        }

        if (Frame != null)
        {
            Frame.NumBlobs = 0;
            Frame.delta_X = 0;
            Frame.delta_Y = 0;
        }

        if (Blobs != null)
        {
            Blobs.Clear();
        }
    }

}

public class MatrixData
{
    public List<int> Values { get; set; } = new List<int>();
}

public class FrameData
{
    public int NumBlobs { get; set; }
    public double delta_X { get; set; }
    public double delta_Y { get; set; }
}

public class BlobsData
{
    public int ID { get; set; }
    public double X { get; set; }
    public double Y { get; set; }
    public double Height { get; set; }
    public double Speed { get; set; }
    public double Direction { get; set; }
}


namespace STM32Visualization
{

    public partial class MainWindow : Window
    {
        private DataPacket parsedData;
        private SerialPort _serialPort;
        private bool _isReading = false;
        private StringBuilder _buffer = new StringBuilder(); //TODO: Global string class that stores the current frames
        private CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
        private bool isToggleOn = false;




        public MainWindow()
        {
            InitializeComponent();
            PopulateComboBoxes();
            parsedData = new DataPacket();


            DispatcherTimer timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(25)
            };
            timer.Tick += ProcessBuffer;
            timer.Start();
        }

        private void ProcessBuffer(object sender, EventArgs e)
        {
            if (_buffer.Length > 0)
            {
                // Add text
                richTextBox.AppendText(_buffer.ToString());
                _buffer.Clear();

                // Auto-scroll
                richTextBox.ScrollToEnd();

                // Trim excess content
                TrimExcessContent(richTextBox, 50);  // Keep the last 50 lines
            }
        }

        private void TrimExcessContent(RichTextBox richTextBox, int maxLineCount)
        {
            var text = new TextRange(richTextBox.Document.ContentStart, richTextBox.Document.ContentEnd).Text;
            var lines = text.Split(new[] { Environment.NewLine }, StringSplitOptions.None);

            if (lines.Length > maxLineCount)
            {
                var startIndex = lines.Length - maxLineCount;
                var trimmedContent = string.Join(Environment.NewLine, lines, startIndex, maxLineCount);

                richTextBox.Document.Blocks.Clear();
                richTextBox.AppendText(trimmedContent);
            }
        }


        private void PopulateComboBoxes()
        {
            // Populate the COM ports dropdown
            string[] ports = SerialPort.GetPortNames();
            foreach (string port in ports)
            {
                comboBoxComPorts.Items.Add(port);
                comboBoxComPorts.SelectedItem = port;
            }

            // Populate the baud rates dropdown
            int[] baudRates = { 9600, 14400, 19200, 38400, 57600, 115200, 1000000 };
            foreach (int rate in baudRates)
            {
                comboBoxBaudRates.Items.Add(rate);
            }
            comboBoxBaudRates.SelectedItem = 1000000;  // Set default value
        }

        private void btnStartStop_Click(object sender, RoutedEventArgs e)
        {
            ////Debug.writeLine("Button clicked");
            if (!_isReading)
            {
                // Initialize serial port and begin reading
                _serialPort = new SerialPort(comboBoxComPorts.SelectedItem.ToString(), int.Parse(comboBoxBaudRates.SelectedItem.ToString()));
                //_serialPort.DataReceived += _serialPort_DataReceived;
                _serialPort.Open();
                Task.Run(() => PollingMethod(cancellationTokenSource.Token));
                _isReading = true;
                btnStartStop.Content = "Stop";
            }
            else
            {
                cancellationTokenSource.Cancel();
                SafeClose();
            }
        }

        private void _serialPort_DataReceived(object sender, SerialDataReceivedEventArgs e)// This function will be called upon receving data, not being linked at the moment
        {
            // Read the incoming data
            string data = _serialPort.ReadLine();
            data.Trim();
            System.Threading.Tasks.Task.Run(() => ParseData(data));
            //Debug.WriteLine(data);
            //Debug.WriteLine("--------------");
        }

        private async Task PollingMethod(CancellationToken token)// This function will be called when the timer ticks
        {

            while (!token.IsCancellationRequested) // Infinite loop to keep polling. Use a termination condition to stop.
            {
                if (true)
                {
                    string data = _serialPort.ReadLine();
                    //Debug.WriteLine(data);
                    //Debug.WriteLine("======");
                    if (!isToggleOn)
                    {
                        _buffer.AppendLine(data);
                    }
                    if (!string.IsNullOrEmpty(data))
                    {
                        ParseData(data);

                    }
                }
                //await Task.Delay(1); // Delay for 1ms. Adjust this interval as necessary.
            }
        }

        private void SafeClose()
        {
            if (_serialPort != null && _serialPort.IsOpen)
            {
                //_serialPort.DataReceived -= _serialPort_DataReceived;
                _serialPort.Close();
                _serialPort.Dispose();
                _serialPort = null;
                _isReading = false;
                btnStartStop.Content = "Start";
            }
        }

        private async void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (_isReading)
            {
                e.Cancel = true; // Delay the window closing
                await System.Threading.Tasks.Task.Run(() => SafeClose());
                Close();  // Retry closing the window
            }
        }

        private DataPacket ParseData(string data)
        {
            //DataPacket packet = new DataPacket();

            var sections = data.Split(new string[] { "@" }, StringSplitOptions.RemoveEmptyEntries); // not using

            foreach (var section in sections)
            {
                try
                {
                    if (section.StartsWith("##Matrix##"))
                    {
                        ////Debug.writeLine("matrix");
                        var matrixData = section.Substring("##Matrix##".Length);
                        var values = matrixData.Split('|', StringSplitOptions.RemoveEmptyEntries);
                        int counter = 0;
                        foreach (var value in values)
                        {
                            if (counter++ < 64)
                            {
                                parsedData.Matrix.Values.Add(int.Parse(value));
                            }
                        }
                        //Dispatcher.Invoke(() => DrawHeatmap(parsedData.Matrix));

                    }
                    else if (section.StartsWith("##FRAME##"))
                    {
                        ////Debug.writeLine("frame");
                        var frameData = section.Substring("##FRAME##".Length);
                        var values = frameData.Split('|', StringSplitOptions.RemoveEmptyEntries);

                        parsedData.Frame.NumBlobs = int.Parse(values[0]);
                        parsedData.Frame.delta_X = double.Parse(values[1]);
                        parsedData.Frame.delta_Y = double.Parse(values[2]);

                    }
                    else if (section.StartsWith("##BLOBS##"))
                    {
                        ////Debug.writeLine("blobs");
                        var blobsData = section.Substring("##BLOBS##".Length);
                        var blobsSections = blobsData.Split(new string[] { "###" }, StringSplitOptions.RemoveEmptyEntries);

                        foreach (var blobSection in blobsSections)
                        {
                            var values = blobSection.Split('|', StringSplitOptions.RemoveEmptyEntries);
                            BlobsData blob = new BlobsData()
                            {
                                ID = int.Parse(values[0]),
                                X = double.Parse(values[1]),
                                Y = double.Parse(values[2]),
                                Height = double.Parse(values[3]),
                                Speed = double.Parse(values[4]),
                                Direction = double.Parse(values[5])
                            };
                            parsedData.Blobs.Add(blob);
                        }

                    }
                    else if (section.StartsWith("NEWFRAMEEND"))
                    {
                        ////Debug.writeLine("newframeend");
                        this.Dispatcher.Invoke(() =>
                        {
                            PopulateLegendCanvas(0, 2500);
                            DrawHeatmap(parsedData.Matrix);
                            DrawFrameArrow(parsedData.Frame);
                            DrawBlobs(parsedData.Blobs);

                        });
                    }
                    else if (section.StartsWith("NEWFRAME"))
                    {
                        ////Debug.writeLine("newframe");
                        if (isToggleOn && (parsedData.Blobs != null && parsedData.Blobs.Count > 0))
                        {
                            _buffer.AppendLine(parsedData.ToString());
                        }
                        parsedData.Clear();
                    }
                }
                catch (FormatException)
                {
                    Debug.WriteLine(section);
                }
                finally { }
            }

            //_buffer.AppendLine(parsedData.ToString());

            return parsedData;
        }

        private void DrawHeatmap(MatrixData matrixData)
        {
            if (heatmapGrid.Children.Count > 128)
            {
                heatmapGrid.Children.RemoveRange(0, 64);
            }
            var min = 0;        //min max value for heatmap
            var max = 2500;
            if (matrixData != null)
            {
                for (int row = 0; row < 8; row++)
                {
                    for (int col = 0; col < 8; col++)
                    {
                        Rectangle cell = new Rectangle();
                        if (matrixData.Values.Count == 64)
                            cell.Fill = new SolidColorBrush(GetColorForValue(matrixData.Values[row * 8 + col], min, max));

                        Grid.SetRow(cell, row);
                        Grid.SetColumn(cell, col);
                        heatmapGrid.Children.Add(cell);
                    }
                }
            }
        }

        private void DrawBlobs(List<BlobsData> blobsDataList)
        {
            blobCanvas.Children.Clear();
            foreach (var blobsData in blobsDataList)
            {
                // Drawing blob
                Ellipse blob = new Ellipse();
                blob.Width = 10; // Assuming value corresponds to size
                blob.Height = 10;
                blob.Fill = new SolidColorBrush(Color.FromArgb(200, 10, 10, 50));
                Canvas.SetLeft(blob, (0.5 + blobsData.Y) * blobCanvas.ActualWidth / 8 - 5);
                Canvas.SetTop(blob, (0.5 + blobsData.X) * blobCanvas.ActualHeight / 8 - 5);
                blobCanvas.Children.Add(blob);
                // Drawing arrow for direction and speed
                Line arrow = new Line
                {
                    Stroke = Brushes.Black,
                    X1 = (0.5 + blobsData.Y) * blobCanvas.ActualWidth / 8,
                    Y1 = (0.5 + blobsData.X) * blobCanvas.ActualHeight / 8,
                    X2 = (0.5 + blobsData.Y) * blobCanvas.ActualWidth / 8 + Math.Sin(Math.PI * blobsData.Direction / 180) * blobsData.Speed * 100,
                    Y2 = (0.5 + blobsData.X) * blobCanvas.ActualHeight / 8 + Math.Cos(Math.PI * blobsData.Direction / 180) * blobsData.Speed * 100,
                    StrokeThickness = 5
                };
                blobCanvas.Children.Add(arrow);
                // Drawing blob information
                TextBlock blobInfo = new TextBlock
                {
                    Text = $"X: {blobsData.X}, Y: {blobsData.Y}\nDir: {blobsData.Direction}°, Spd: {blobsData.Speed}",
                    Foreground = Brushes.Black
                };
                Canvas.SetLeft(blobInfo, (0.5 + blobsData.Y) * blobCanvas.ActualWidth / 8 + 10);  // Positioning to the right of the blob
                Canvas.SetTop(blobInfo, (0.5 + blobsData.X) * blobCanvas.ActualHeight / 8);
                blobCanvas.Children.Add(blobInfo);
            }
        }


        private Color GetColorForValue(int value, int minValue, int maxValue)
        {
            // Ensure value is within range
            value = Math.Max(minValue, Math.Min(value, maxValue));

            byte alpha = 255;
            double ratio = (double)(value - minValue) / (maxValue - minValue);

            byte red = (byte)(120 - 70 * ratio);
            byte green = (byte)(255 - 205 * ratio);
            byte blue = (byte)(200 + 55 * ratio);

            return Color.FromArgb(alpha, red, green, blue);

        }

        private void DrawFrameArrow(FrameData frameData)
        {

            frameCanvas.Children.Clear();
            double centerX = frameCanvas.Width / 2;
            double centerY = frameCanvas.Height / 2;

            double scaleFactor = 50;  // Adjust this value to properly scale the arrows

            Line arrow = new Line
            {
                Stroke = Brushes.Black,
                X1 = centerX,
                Y1 = centerY,
                X2 = centerX + frameData.delta_X * scaleFactor,
                Y2 = centerY - frameData.delta_Y * scaleFactor,  // Subtracting because canvas Y-coordinate is inverted
                StrokeThickness = 2
            };
            frameCanvas.Children.Add(arrow);
            // Drawing delta information
            TextBlock deltaInfo = new TextBlock
            {
                Text = $"Δx: {frameData.delta_X}, Δy: {frameData.delta_Y}",
                Foreground = Brushes.Black
            };
            Canvas.SetLeft(deltaInfo, 0);
            Canvas.SetBottom(deltaInfo, 0);
            frameCanvas.Children.Add(deltaInfo);
        }

        private void PopulateLegendCanvas(int minValue, int maxValue)
        {
            // Clear the existing children
            legendCanvas.Children.Clear();

            // Create the gradient rectangle
            Rectangle gradientRect = new Rectangle
            {
                Width = legendCanvas.ActualWidth,
                Height = legendCanvas.ActualHeight,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            // Define the gradient
            LinearGradientBrush gradientBrush = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0.5), // Left
                EndPoint = new Point(1, 0.5), // right
                GradientStops = new GradientStopCollection
        {
            new GradientStop(Color.FromArgb(255, 120, 255, 200), 0),  // Green
            new GradientStop(Color.FromArgb(255, 50, 50, 255), 1)   // Blue
        }
            };
            gradientRect.Fill = gradientBrush;
            legendCanvas.Children.Add(gradientRect);

            // Add minimum value label
            TextBlock minLabel = new TextBlock
            {
                Text = minValue.ToString()
            };

            Canvas.SetLeft(minLabel, 0);
            Canvas.SetTop(minLabel, (legendCanvas.ActualHeight - minLabel.ActualHeight) / 2); // Vertically center

            legendCanvas.Children.Add(minLabel);

            // Add maximum value label
            TextBlock maxLabel = new TextBlock
            {
                Text = maxValue.ToString()
            };

            Canvas.SetRight(maxLabel, 0);
            Canvas.SetTop(maxLabel, (legendCanvas.ActualHeight - maxLabel.ActualHeight) / 2); // Vertically center

            // We need to wait for the `maxLabel` to be rendered to get its actual width
            maxLabel.Loaded += (sender, e) =>
            {
                Canvas.SetLeft(maxLabel, legendCanvas.ActualWidth - maxLabel.ActualWidth);
            };

            legendCanvas.Children.Add(maxLabel);
        }

        private void ToggleCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            isToggleOn = true;
        }

        private void ToggleCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            isToggleOn = false;
        }




        private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            double targetRatio = 1;  // 1:1 ratio for a square window
            double currentRatio = e.NewSize.Width / e.NewSize.Height;

            if (currentRatio > targetRatio)
            {
                this.Width = e.NewSize.Height;
            }
            else
            {
                this.Height = e.NewSize.Width;
            }
        }


    }
}



