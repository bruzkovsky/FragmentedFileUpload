using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;
using FragmentedFileUpload;
using FragmentedFileUpload.Client;

// sample image file: http://hubblesite.org/newscenter/archive/releases/2004/15/image/b/
namespace WinFormsFileUpload
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

            var tempFolderPath = Path.Combine(CurrentFolder, "Temp");
            var splitter = FileSplitter.Create(openFileDialog1.FileName);
            splitter.TempFolderPath = tempFolderPath;
            splitter.MaxChunkSizeMegaByte = 0.1;
            splitter.SplitFile();
            foreach (var file in splitter.FileParts)
            {
                if (!await UploadFile(file))
                    break;
            }
            MessageBox.Show("Upload complete!");
        }

        public async Task<bool> UploadFile(string fileName)
        {
            const string requestUri = "http://localhost:8170/Home/UploadFile/";
            var uploadClient = UploadClient.Create(fileName, requestUri);
            return await uploadClient.UploadFile();
        }
        
    }

}
