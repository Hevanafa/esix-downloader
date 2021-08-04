using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows;

namespace EsixDownloader
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            if (!Directory.Exists("Downloads"))
                Directory.CreateDirectory("Downloads");
        }

        const string ChromeUserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/92.0.4515.131 Safari/537.36";

        async void startDownload()
        {
            prgDownload.Value = 0;

            try
            {
                var uri = new Uri(txbUri.Text);

                if (!uri.Host.Contains("e621.net"))
                    throw new ArgumentException("The inputted URL is not part of e621.net.");

                if (!txbUri.Text.Contains("e621.net/posts"))
                    throw new ArgumentException("The inputted URL should be a link to an e621 post.");

                txbOutput.Text += "Starting download...\n";

                var req = (HttpWebRequest)WebRequest.Create(uri);
                req.AutomaticDecompression = DecompressionMethods.Deflate | DecompressionMethods.GZip;
                req.UserAgent = ChromeUserAgent;

                var res = await req.GetResponseAsync();
                var sr = new StreamReader(res.GetResponseStream());
                var HtmlContent = await sr.ReadToEndAsync();

                // Done: get artists' names
                var artistTagList = Regex.Match(
                    HtmlContent,
                    @"(?<=<ul class=""artist-tag-list"">)(.*?)(?=</ul>)",
                    RegexOptions.Singleline
                ).Value;

                var anchors = Regex.Matches(
                    artistTagList,
                    "<a(.*?)>(.*?)</a>",
                    RegexOptions.Singleline
                ).Cast<Match>()
                    .Select(x => x + "")
                    .ToArray();

                var artists = anchors
                    .Where(x => x.Contains("itemprop=\"author\""))
                    .Select(anchorTag =>
                        Regex.Match(
                            anchorTag,
                            "(?<=<a(.*?)>)(.*?)(?=</a>)"
                        ).Value
                    ).ToArray();

                var saveFilename = string.Join(",", artists)
                    + " - " +
                    Path.GetFileName(uri.GetLeftPart(UriPartial.Path));

                //Debug.Print(saveFilename);

                // Dispose everything
                req = null;
                res.Dispose();
                sr.Dispose();

                txbOutput.Text += $"The page has been fully received ({HtmlContent.Length} chars).\n";

                //image-download-link
                var anchor = Regex.Match(
                    HtmlContent,
                    @"(?<=<div id=""image-download-link"">)(.*?)(?=<\/div>)",
                    RegexOptions.Singleline
                ).Value;

                var href = Regex.Match(
                    anchor,
                    @"(?<=href="")(.*?)(?="")",
                    RegexOptions.Singleline
                ).Value;

                txbOutput.Text += $"Found the download link:\n{href}\n";

                var downloadLink = new Uri(href);
                var ext = Path.GetExtension(href);

                var targetFilename = Path.Combine(
                    "Downloads",
                    saveFilename + ext
                //Path.GetFileName(href)
                );

                txbOutput.Text += "Starting download...\n";

                req = (HttpWebRequest)WebRequest.Create(downloadLink);
                res = await req.GetResponseAsync();

                txbOutput.Text += $"File size: {res.ContentLength} bytes\n\n" +
                    $"Saving file as \"{targetFilename}\"\n";

                req = null;
                res.Dispose();

                //txbOutput.Text += "Starting a new download thread\n";

                //Thread.CurrentThread.IsBackground = true;

                // https://stackoverflow.com/questions/1585985/how-to-use-the-webclient-downloaddataasync-method-in-this-context
                using (var wc = new WebClient())
                {
                    wc.DownloadProgressChanged += Wc_DownloadProgressChanged;

                    await wc.DownloadFileTaskAsync(downloadLink, "temp_download");
                    //wc.DownloadDataCompleted += (sender, e) =>
                    //{
                    //};

                    //txbOutput.Text += "Writing to file...\n";

                    //var fileData = e.Result;

                    // var fs = new FileStream("temp_download", FileMode.Create);
                    // await fs.WriteAsync(fileData, 0, fileData.Length);
                    // fs.Close();
                    // fs.Dispose();

                    // wc.DownloadData(downloadLink);
                }

                if (File.Exists(targetFilename))
                    File.Delete(targetFilename);

                File.Move("temp_download", targetFilename);

                txbOutput.Text += "Download is completed.\n";

                //wc.Dispose();
                //}).Start();
            }
            catch (Exception ex)
            {
                txbOutput.Text += "Error: " + ex.Message + "\n";
            }
            finally
            {
                btnDownload.IsEnabled = true;
            }
        }

        private void Wc_DownloadProgressChanged(object sender, DownloadProgressChangedEventArgs e)
        {
            prgDownload.Value = e.BytesReceived / (double)e.TotalBytesToReceive * 100d;
        }

        private void Wc_DownloadFileCompleted(object sender, System.ComponentModel.AsyncCompletedEventArgs e)
        {
            prgDownload.Value = 100d;
            txbOutput.Text += "Download is completed.\n";
        }

        private void BtnDownload_Click(object sender, RoutedEventArgs e)
        {
            btnDownload.IsEnabled = false;
            startDownload();
        }

        private void BtnDownloadsFolder_Click(object sender, RoutedEventArgs e)
        {
            Process.Start(
                "explorer.exe",

                Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory,
                    "Downloads"
                )
            );
        }

        private void TxbOutput_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            txbOutput.ScrollToEnd();
        }
    }
}
