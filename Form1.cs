using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace YapcarSpriteViewer
{
    public partial class Form1 : Form
    {
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
                SpriteList.Clear();
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


        public void parsedSprite(BinaryReader reader)
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
                listBox1.Items.Add(i+1);
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

            Bitmap bitmap = CreateImageFromSprite(SpriteList[0]);
            pictureBox1.Image = bitmap;
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
            //배경 흰색으로 변경
            if (pixelData == 0xF81F)
            {
                pixelData = 0xFFFF;
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
    }


    public class Sprite
    {
        public int Type { get; set; }
        public short Width { get; set;}
        public short Height { get; set;}
        public byte[] Data { get; set; }
    }

}
