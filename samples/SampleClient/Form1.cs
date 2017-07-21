using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using FragmentedFileUpload.Client;

// sample image file: http://hubblesite.org/newscenter/archive/releases/2004/15/image/b/
namespace SampleClient
{
    public partial class Form1 : Form
    {
        string CurrentFolder = AppDomain.CurrentDomain.BaseDirectory;

        public Form1()
        {
            InitializeComponent();
        }

        private async void button3_Click(object sender, EventArgs e)
        {
            var rslt = openFileDialog1.ShowDialog();
            if (rslt != DialogResult.OK)
                return;

            if (await UploadFile(openFileDialog1.FileName))
                MessageBox.Show("Upload complete.");
            else
                MessageBox.Show("Error while uploading.");
        }

        public async Task<bool> UploadFile(string fileName)
        {
            var tempFolderPath = Path.Combine(CurrentFolder, "Temp");
            const string requestUri = "http://localhost:8170/Home/UploadFile/";
            var uploadClient = UploadClient.Create(fileName, requestUri, tempFolderPath);
            return await uploadClient.UploadFile(CancellationToken.None);
        }
        
    }

}
