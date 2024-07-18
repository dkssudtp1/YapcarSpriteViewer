using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using static System.Net.Mime.MediaTypeNames;
using Microsoft.WindowsAPICodePack.Dialogs;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.Window;

namespace YapcarSpriteViewer
{
    public partial class Form1 : Form
    {
        public List<List<Sprite>> SpriteLists = new List<List<Sprite>>();
        public List<Sprite> SpriteList = new List<Sprite>();
        public int type = 0;
        public int imageCount = 0;
        public int imageFrame = 0;
        public float zoomFactor = 1.0f;

        public Form1()
        {
            InitializeComponent();
            pictureBox1.MouseWheel += pictureBox1_MouseWheel;
        }



        private void button1_Click(object sender, EventArgs e)
        {
            var fileContent = string.Empty;
            var filePath = string.Empty;

            using (OpenFileDialog openFileDialog = new OpenFileDialog())
            {
                SpriteList = new List<Sprite>();
                listBox1.Items.Clear();
                panel1.Refresh();
                pictureBox1.Refresh();
                openFileDialog.Filter = "spr files (*.spr)|*.spr|All files (*.*)|*.*";
                openFileDialog.FilterIndex = 2;
                openFileDialog.RestoreDirectory = true;

                if (openFileDialog.ShowDialog() == DialogResult.OK)
                {
                    filePath = openFileDialog.FileName;
                    textBox1.Text = openFileDialog.FileName;
                    using (BinaryReader reader = new BinaryReader(File.Open(filePath, FileMode.Open)))
                    {
                        parsedSprite(reader);
                    }
                }
            }
        }


        public void parsedSprite(BinaryReader reader, bool folder = false)
        {
            try
            {
                //얍카 SPR 파일인지 체크
                if (!System.Text.Encoding.ASCII.GetString(reader.ReadBytes(4)).Equals("ISPR"))
                {
                    return;
                }


                type = reader.ReadInt32(); //0 = RGB565, 1 = RGBA4444 

                reader.ReadInt32(); //unknown
                imageCount = reader.ReadInt16();
                imageFrame = reader.ReadInt16(); //unknown
                reader.ReadBytes(408); //unknown
                for (int i = 0; i < imageCount; i++)
                {
                    Sprite sprite = new Sprite();
                    sprite.Type = type;
                    sprite.Width = reader.ReadInt16();
                    SpriteList.Add(sprite);
                    listBox1.Items.Add(i + 1);
                }
                for (int i = 0; i < imageCount; i++)
                {
                    SpriteList[i].Height = reader.ReadInt16();
                }
                reader.ReadBytes(14 * imageFrame);

                for (int i = 0; i < imageCount; i++)
                {
                    SpriteList[i].Data = reader.ReadBytes(SpriteList[i].Width * SpriteList[i].Height * 2);
                }

                if (folder)
                {
                    SpriteLists.Add(SpriteList);
                }

                Bitmap bitmap = CreateImageFromSprite(SpriteList[0]);
                pictureBox1.Image = bitmap;
            }
            catch (Exception ex)
            {
            }
        }

        private void listBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            try
            {
                if (listBox1.SelectedItem != null)
                {
                    if (listBox1.SelectedIndex < SpriteList.Count)
                    {
                        Bitmap bitmap = CreateImageFromSprite(SpriteList[listBox1.SelectedIndex]);
                        pictureBox1.Width = bitmap.Width;
                        pictureBox1.Height = bitmap.Height;
                        pictureBox1.Image = bitmap;
                    }
                }
            }
            catch { }
        }

        public Bitmap CreateImageFromSprite(Sprite sprite)
        {
            if (sprite.Data == null || sprite.Data.Length != sprite.Width * sprite.Height * 2)
            {
                return null;
            }

            Bitmap image = new Bitmap(sprite.Width, sprite.Height, PixelFormat.Format32bppArgb);
            int dataIndex = 0;

            for (int y = 0; y < sprite.Height; y++)
            {
                for (int x = 0; x < sprite.Width; x++)
                {
                    ushort pixelData = (ushort)(sprite.Data[dataIndex++] | (sprite.Data[dataIndex++] << 8));
                    Color color = Color.Empty;

                    switch (sprite.Type)
                    {
                        case 1: // RGBA4444
                            color = DecodeRGBA4444(pixelData);
                            break;
                        case 0: // RGB565
                            color = DecodeRGB565(pixelData);
                            break;
                    }

                    image.SetPixel(x, y, color);
                }
            }

            if (zoomFactor != 1.0f)
            {
                return ResizeImage(image, zoomFactor);
            }

            return image;
        }

        public Bitmap CreateAtlasFromSpriteLists(List<List<Sprite>> spriteLists)
        {
            if (spriteLists == null || spriteLists.Count == 0)
            {
                return null;
            }

            int atlasWidth = 0;
            int atlasHeight = 0;

            foreach (List<Sprite> spriteList in spriteLists)
            {
                int rowWidth = 0;
                int rowHeight = 0;

                foreach (Sprite sprite in spriteList)
                {
                    if (sprite.Data == null || sprite.Data.Length != sprite.Width * sprite.Height * 2)
                    {
                        return null;
                    }

                    rowWidth += sprite.Width;
                    rowHeight = Math.Max(rowHeight, sprite.Height);
                }

                atlasWidth = Math.Max(atlasWidth, rowWidth);
                atlasHeight += rowHeight;
            }

            Bitmap atlas = new Bitmap(atlasWidth, atlasHeight, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(atlas))
            {
                int currentX = 0;
                int currentY = 0;

                foreach (List<Sprite> spriteList in spriteLists)
                {
                    currentX = 0; 

                    foreach (Sprite sprite in spriteList)
                    {
                        if (sprite.Data == null || sprite.Data.Length != sprite.Width * sprite.Height * 2)
                        {
                            return null;
                        }

                        Bitmap spriteImage = CreateImageFromSprite(sprite);
                        g.DrawImage(spriteImage, currentX, currentY);
                        currentX += spriteImage.Width;
                    }

                    currentY += spriteList.Max(s => s.Height);
                }
            }

            if (zoomFactor != 1.0f)
            {
                return ResizeImage(atlas, zoomFactor);
            }

            return atlas;
        }

        private Color DecodeRGBA4444(ushort pixelData)
        {
            int a = ((pixelData >> 12) & 0xF) * 255 / 15;
            int r = ((pixelData >> 8) & 0xF) * 255 / 15;
            int g = ((pixelData >> 4) & 0xF) * 255 / 15;
            int b = ((pixelData >> 0) & 0xF) * 255 / 15;

            return Color.FromArgb(a, r, g, b);
        }

        private Color DecodeRGB565(ushort pixelData)
        {
            //배경 투명으로 변경
            if (pixelData == 0xF81F)
            {
                return Color.FromArgb(0x0, 0xFF, 0xFF, 0xFF);
            }
            int b = ((pixelData >> 0) & 0x1F) * 255 / 31;
            int g = ((pixelData >> 5) & 0x3F) * 255 / 63;
            int r = ((pixelData >> 11) & 0x1F) * 255 / 31;

            return Color.FromArgb(0xFF, r, g, b);
        }

        private Bitmap ResizeImage(Bitmap image, float zoomFactor)
        {
            int newWidth = (int)Math.Round(image.Width * zoomFactor);
            int newHeight = (int)Math.Round(image.Height * zoomFactor);

            Bitmap resizedBitmap = new Bitmap(newWidth, newHeight);
            using (Graphics g = Graphics.FromImage(resizedBitmap))
            {
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                g.DrawImage(image, 0, 0, newWidth, newHeight);
            }
            return resizedBitmap;
        }

        private void pictureBox1_MouseWheel(object sender, MouseEventArgs e)
        {
            // 휠 회전 방향 확인
            if (e.Delta > 0)
            {
                zoomFactor += 0.1f; // 확대 비율 조절
            }
            else
            {
                zoomFactor -= 0.1f; // 축소 비율 조절
            }

            // 최소/최대 배율 제한
            zoomFactor = Math.Max(0.1f, zoomFactor);
            zoomFactor = Math.Min(10.0f, zoomFactor);

            //리스트 선택 안했을시 강제로 0
            if (listBox1.SelectedIndex == -1)
            {
                listBox1.SelectedIndex = 0;
            }

            Bitmap bitmap = CreateImageFromSprite(SpriteList[listBox1.SelectedIndex]);
            pictureBox1.Image = bitmap;
        }

        private void button2_Click(object sender, EventArgs e)
        {

            string folderPath = Path.GetDirectoryName(textBox1.Text);
            string outputFolder = Path.Combine(folderPath, "output");

            if (!Directory.Exists(outputFolder))
            {
                Directory.CreateDirectory(outputFolder);
            }

            int index = 0;
            foreach (Sprite sprite in SpriteList)
            {
                Bitmap bitmap = CreateImageFromSprite(sprite);
                string filePath = Path.Combine(outputFolder, $"{index}.png");
                bitmap.Save(filePath, ImageFormat.Png);
                index++;
            }
            DialogResult result = MessageBox.Show("저장이 완료되었습니다. \n 저장폴더를 확인하시겠습니까?", "Save", MessageBoxButtons.YesNo, MessageBoxIcon.Information);

            if (result == DialogResult.Yes)
            {
                Process.Start("explorer.exe", outputFolder);
            }
        }

        private void button3_Click(object sender, EventArgs e)
        {
            using (CommonOpenFileDialog dialog = new CommonOpenFileDialog())
            {
                dialog.IsFolderPicker = true;
                SpriteLists = new List<List<Sprite>>();
                if (dialog.ShowDialog() == CommonFileDialogResult.Ok)
                {
                    string folderPath = dialog.FileName;
                    textBox1.Text = folderPath;

                    // Get all .spr files in the selected folder
                    string[] files = Directory.GetFiles(folderPath, "*.spr");

                    foreach (string filePath in files)
                    {
                        SpriteList = new List<Sprite>();
                        using (BinaryReader reader = new BinaryReader(File.Open(filePath, FileMode.Open)))
                        {
                            parsedSprite(reader, true);
                        }
                    }
                    Bitmap bitmap = CreateAtlasFromSpriteLists(SpriteLists);

                    string savePath = Path.Combine(folderPath, "atlas.png");
                    bitmap.Save(savePath, ImageFormat.Png);

                    DialogResult result = MessageBox.Show("저장이 완료되었습니다. \n 저장폴더를 확인하시겠습니까?", "Save", MessageBoxButtons.YesNo, MessageBoxIcon.Information);

                    if (result == DialogResult.Yes)
                    {
                        Process.Start("explorer.exe", folderPath);
                    }
                }
            }
        }
    }


    public class Sprite
    {
        public int Type { get; set; }
        public short Width { get; set; }
        public short Height { get; set; }
        public byte[] Data { get; set; }
    }

}
