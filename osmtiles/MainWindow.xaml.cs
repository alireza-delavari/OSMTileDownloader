using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace osmtiles
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        string _baseDirectory = @"out3\";
        bool _cancelFlag = false;
        public MainWindow()
        {
            InitializeComponent();
        }

        private void BtnDownload_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _cancelFlag = false;
                if (chbGetFromLatLong.IsChecked.GetValueOrDefault(false))
                {
                    DownloadLatLong(
                        double.Parse(txtLatTL.Text)
                        , double.Parse(txtLongTL.Text)
                        , double.Parse(txtLatBR.Text)
                        , double.Parse(txtLongBR.Text)
                        , int.Parse(txtZoomLevel.Text));
                }
                else
                {
                    DownloadAll(int.Parse(txtZoomLevel.Text));
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
                MessageBox.Show(ex.ToString());
            }
        }

        public Point WorldToTilePos(double lon, double lat, int zoom)
        {
            Point p = new Point();
            p.X = (float)((lon + 180.0) / 360.0 * (1 << zoom));
            p.Y = (float)((1.0 - Math.Log(Math.Tan(lat * Math.PI / 180.0) +
                1.0 / Math.Cos(lat * Math.PI / 180.0)) / Math.PI) / 2.0 * (1 << zoom));

            return p;
        }
        public Point TileToWorldPos(double tile_x, double tile_y, int zoom)
        {
            Point p = new Point();
            double n = Math.PI - ((2.0 * Math.PI * tile_y) / Math.Pow(2.0, zoom));

            p.X = (float)((tile_x / Math.Pow(2.0, zoom) * 360.0) - 180.0);
            p.Y = (float)(180.0 / Math.PI * Math.Atan(Math.Sinh(n)));

            return p;
        }

        public async void DownloadAll(int zoom)
        {
            int minx = 0;
            int maxx = (int)Math.Pow((double)2, (double)zoom);
            int miny = 0;
            int maxy = maxx;
            int totalTiles = maxx * maxy;
            int threadsCount = int.TryParse(txtThreadCount.Text.ToString(), out int thCount) ? thCount : 2;

            progressAllFile.Maximum = totalTiles;
            progressAllFile.Value = 0;

            var tStart = DateTime.Now;
            txtOutput.Text += Environment.NewLine + tStart.ToString("hh:mm:ss ffff");
            txtOutput.Text += Environment.NewLine + ((tStart.Hour * 60 * 60) + (tStart.Minute * 60) + tStart.Second).ToString();

            txtStatus.Text = "Downloading";
            await Task.Run(async () =>
            {
                await DownloadRangeAsync(zoom, minx, maxx, miny, maxy, threadsCount, _baseDirectory);
            });
            //await DownloadRange(zoom, minx, maxx, miny, maxy);
            txtStatus.Text = "Complete";

            var tEnd = DateTime.Now;
            txtOutput.Text += Environment.NewLine + tEnd.ToString("hh:mm:ss ffff");
            txtOutput.Text += Environment.NewLine + ((tEnd.Hour * 60 * 60) + (tEnd.Minute * 60) + tEnd.Second).ToString();

            if (_cancelFlag)
            {
                _cancelFlag = false;
            }
        }

        public async void DownloadLatLong(double latT, double longL, double latB, double longR, int zoom)
        {
            //var pTL = WorldToTilePos(latL, longT, 4);
            //var pTR = WorldToTilePos(latR, longT, 4);
            //var pBL = WorldToTilePos(latL, longB, 4);
            //var pBR = WorldToTilePos(latR, longB, 4);
            var pTL = WorldToTilePos(longL, latT, zoom);
            var pTR = WorldToTilePos(longR, latT, zoom);
            var pBL = WorldToTilePos(longL, latB, zoom);
            var pBR = WorldToTilePos(longR, latB, zoom);
            int minx = (int)Math.Min(pTL.X, pTR.X);
            int maxx = (int)Math.Max(pTL.X, pTR.X);
            int miny = (int)Math.Min(pTL.Y, pBL.Y);
            int maxy = (int)Math.Max(pTL.Y, pBL.Y);
            int totalTiles = (maxx - minx) * (maxy - miny);
            int threadsCount = int.TryParse(txtThreadCount.Text.ToString(), out int thCount) ? thCount : 2;
            ////string url = @"https://a.tile.openstreetmap.org/${z}/${x}/${y}.png";
            progressAllFile.Maximum = totalTiles;
            progressAllFile.Value = 0;

            var tStart = DateTime.Now;
            txtOutput.Text += Environment.NewLine + tStart.ToString("hh:mm:ss ffff");
            txtOutput.Text += Environment.NewLine + ((tStart.Hour * 60 * 60) + (tStart.Minute * 60) + tStart.Second).ToString();

            txtStatus.Text = "Downloading";
            await Task.Run(async () =>
            {
                await DownloadRangeAsync(zoom, minx, maxx, miny, maxy, threadsCount, _baseDirectory);
            });
            //await DownloadRange(zoom, minx, maxx, miny, maxy);
            txtStatus.Text = "Complete";

            var tEnd = DateTime.Now;
            txtOutput.Text += Environment.NewLine + tEnd.ToString("hh:mm:ss ffff");
            txtOutput.Text += Environment.NewLine + ((tEnd.Hour * 60 * 60) + (tEnd.Minute * 60) + tEnd.Second).ToString();

            if (_cancelFlag)
            {
                _cancelFlag = false;
            }
        }

        private async Task DownloadRangeAsync(int zoom, int minx, int maxx, int miny, int maxy, int threadsCount, string baseDirectory)
        {
            //ThreadPool.SetMaxThreads(2, 2);
            Random random = new Random(DateTime.Now.Millisecond);
            string[] subs = { "a", "b", "c" };
            string url1 = @"https://";
            //string baseDirectory = @"out3\";
            for (double x = minx; x < maxx; x++)
            {
                if (_cancelFlag)
                    break;
                for (double y = miny; y < maxy; y++)
                {
                    try
                    {
                        if (_cancelFlag)
                            break;
                        string url2 = @".tile.openstreetmap.org/" + zoom + "/" + (int)x + "/" + (int)y + ".png";
                        int rnd = random.Next(0, 3);
                        string url = url1 + subs[rnd] + url2;
                        string zoomDirectory = baseDirectory + zoom.ToString() + @"\";
                        string xDirectory = zoomDirectory + x.ToString() + @"\";
                        string filename = xDirectory + y.ToString() + ".png";
                        Directory.CreateDirectory(baseDirectory);
                        Directory.CreateDirectory(zoomDirectory);
                        Directory.CreateDirectory(xDirectory);
                        //string filename2 = baseDirectory + zoom.ToString() + "_" + x.ToString() + "_" + y.ToString() + ".png";
                        if (File.Exists(filename))
                        {
                            if (IsValidImage(filename))
                            {
                                App.Current.Dispatcher.Invoke(() =>
                                {
                                    progressAllFile.Value++;
                                });
                                continue;
                            }
                        }

                        //while (threadsCount < 1) ;
                        //threadsCount--;
                        //ThreadPool.QueueUserWorkItem(async (obj) => { await DownloadFile(url, filename); });
                        if (threadsCount > 1)
                        {
                            threadsCount--;
                            Thread thread = new Thread(async () =>
                            {
                                await DownloadFile(url, filename);
                                threadsCount++;
                            });
                            thread.Start();
                            //threadsCount++;
                        }
                        else
                        {
                            await DownloadFile(url, filename);
                        }

                    }
                    catch (Exception)
                    {

                    }
                }
            }
        }

        private async Task DownloadFile(string url, string filename)
        {
            try
            {
                if (_cancelFlag)
                    return;
                using (WebClient client = new WebClient())
                {
                    //SetHeaders(client);
                    SetHeaders2(client);
                    client.DownloadFileCompleted += Client_DownloadFileCompleted;
                    //Thread.Sleep(800);
                    await client.DownloadFileTaskAsync(new Uri(url), filename);
                    //System.Diagnostics.Debug.WriteLine(url);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex);
            }
        }

        private void Client_DownloadFileCompleted(object sender, System.ComponentModel.AsyncCompletedEventArgs e)
        {
            try
            {
                App.Current.Dispatcher.Invoke(() =>
                {
                    progressAllFile.Value++;
                });
            }
            catch (Exception) { }
        }

        private static void SetHeaders(WebClient client)
        {
            client.Headers.Add(HttpRequestHeader.Accept, "text/html,application/xhtml+xml" +
                                ",application/xml;q=0.9,image/webp,image/apng,*/*;q=0.8,application/signed-exchange;v=b3");
            client.Headers.Add(HttpRequestHeader.AcceptEncoding, "gzip, deflate, br");
            client.Headers.Add(HttpRequestHeader.AcceptLanguage, "en,en-US;q=0.9,fa;q=0.8");
            client.Headers.Add(HttpRequestHeader.CacheControl, "max-age=0");
            client.Headers.Add(HttpRequestHeader.Cookie, "_osm_totp_token=906161");
            //client.Headers.Add(HttpRequestHeader.IfNoneMatch, "a5165d635f3b598a0e063b7a02030a56");
            client.Headers.Add("sec-fetch-mode", "navigate");
            client.Headers.Add("sec-fetch-site", "none");
            client.Headers.Add("sec-fetch-user", "?1");
            //client.Headers.Add("sec-fetch-mode", "navigate");
            client.Headers.Add("upgrade-insecure-requests", "1");
            client.Headers.Add(HttpRequestHeader.UserAgent, "Mozilla/5.0 (Windows NT 10.0; Win64; x64) " +
                "AppleWebKit/537.36 (KHTML, like Gecko) Chrome/76.0.3809.100 Safari/537.36");
        }
        private static void SetHeaders2(WebClient client)
        {
            //client.Headers.Add(HttpRequestHeader.Accept, "text/html,application/xhtml+xml" +
            //                    ",application/xml;q=0.9,image/webp,image/apng,*/*;q=0.8,application/signed-exchange;v=b3");
            client.Headers.Add(HttpRequestHeader.AcceptEncoding, "gzip, deflate, br");
            client.Headers.Add(HttpRequestHeader.AcceptLanguage, "en,en-US;q=0.9,fa;q=0.8");
            //client.Headers.Add(HttpRequestHeader.Cookie, "_osm_totp_token=906161");

            client.Headers.Add(HttpRequestHeader.Referer, "https://www.openstreetmap.org/");
            client.Headers.Add("Sec-Fetch-Mode", "no-cors");
            client.Headers.Add(HttpRequestHeader.UserAgent, "Mozilla/5.0 (Windows NT 10.0; Win64; x64) " +
                "AppleWebKit/537.36 (KHTML, like Gecko) Chrome/76.0.3809.100 Safari/537.36");
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            _cancelFlag = true;
        }

        bool IsValidImage(string filePath)
        {
            var bitmap = new BitmapImage();
            var stream = File.OpenRead(filePath);
            try
            {
                //BitmapImage newImage = new BitmapImage(new Uri(filePath, UriKind.Relative));
                //var format = newImage.Format;
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.StreamSource = stream;
                bitmap.EndInit();
            }
            catch (NotSupportedException)
            {
                // System.NotSupportedException:
                // No imaging component suitable to complete this operation was found.
                bitmap.Freeze();
                stream.Close();
                stream.Dispose();
                return false;
            }
            catch (OutOfMemoryException)
            {
                bitmap.Freeze();
                stream.Close();
                stream.Dispose();
                return false;
            }
            bitmap.Freeze();
            stream.Close();
            stream.Dispose();
            return true;
        }
    }
}
