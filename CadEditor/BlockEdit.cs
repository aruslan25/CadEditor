﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;

namespace CadEditor
{
    public partial class BlockEdit : Form
    {
        public BlockEdit()
        {
            InitializeComponent();
        }

        private void BlockEdit_Load(object sender, EventArgs e)
        {
            cbSubpalette.DrawItem += new DrawItemEventHandler(cbSubpalette_DrawItemEvent);
            videoSprites[0] = videoSprites1;
            videoSprites[1] = videoSprites2;
            videoSprites[2] = videoSprites3;
            videoSprites[3] = videoSprites4;
            dirty = false;
            preparePanel();

            Utils.setCbItemsCount(cbVideo, ConfigScript.videoObjOffset.recCount);
            Utils.setCbItemsCount(cbTileset, ConfigScript.blocksOffset.recCount);
            Utils.setCbItemsCount(cbPalette, ConfigScript.palOffset.recCount);
            Utils.setCbIndexWithoutUpdateLevel(cbLevelSelect, cbLevelSelect_SelectedIndexChanged);
            Utils.setCbIndexWithoutUpdateLevel(cbTileset, cbLevelSelect_SelectedIndexChanged);
            Utils.setCbIndexWithoutUpdateLevel(cbDoor, VisibleOnlyChange_SelectedIndexChanged);
            Utils.setCbIndexWithoutUpdateLevel(cbVideo, VisibleOnlyChange_SelectedIndexChanged);
            Utils.setCbIndexWithoutUpdateLevel(cbPalette, VisibleOnlyChange_SelectedIndexChanged);
            Utils.setCbIndexWithoutUpdateLevel(cbSubpalette, cbSubpalette_SelectedIndexChanged);

            updatePanelsVisible();
            reloadLevel();

            readOnly = Globals.gameType == GameType.DT2;
            btSave.Enabled = !readOnly;
            lbReadOnly.Visible = readOnly;
            btFlipHorizontal.Visible = !readOnly;
            btFlipVertical.Visible = !readOnly;
            btImport.Visible = !readOnly;
            //btExport.Visible = !readOnly;
        }

        private void reloadLevel(bool resetDirty = true)
        {
            setPal();
            setVideo();
            setVideoImage();
            setObjects();
            if (Globals.gameType == GameType.CAD)
              setBack();
            pbBacks.Refresh();
            if (resetDirty)
              dirty = false;
        }

        private void setBack()
        {
            int backAddr = Globals.getBackTileAddr(curActiveLevel);
            for (int i = 0; i < 16; i++)
              curActiveBack[i] = Globals.romdata[backAddr + i];
        }

        private void setPal()
        {
            byte palId;
            if (Globals.gameType == GameType.CAD)
            {
                if (curDoor <= 0)
                    palId = (byte)Globals.levelData[curActiveLevel].palId;
                else
                    palId = (byte)Globals.doorsData[curDoor - 1].palId;
            }
            else
            {
                palId = (byte)curActivePal;
            }
            int addr = Globals.getPalAddr(palId);
            for (int i = 0; i < Globals.PAL_LEN; i++)
                palette[i] = (byte)(Globals.romdata[addr + i] & 0x3F);

            //set image for pallete
            var b = new Bitmap(16 * 16, 16);
            using (Graphics g = Graphics.FromImage(b))
            {
                for (int i = 0; i < Globals.PAL_LEN; i++)
                    g.FillRectangle(new SolidBrush(Video.NesColors[palette[i]]), i * 16, 0, 16, 16);
            }
            paletteMap.Image = b;

            //set images for subpalletes
            subpalSprites.Images.Clear();
            for (int i = 0; i < 4; i++)
            {
                var sb = new Bitmap(16 * 4, 16);
                using (Graphics g = Graphics.FromImage(sb))
                {
                    g.FillRectangle(new SolidBrush(Video.NesColors[palette[i * 4]]), 0, 0, 16, 16);
                    g.FillRectangle(new SolidBrush(Video.NesColors[palette[i * 4 + 1]]), 16, 0, 16, 16);
                    g.FillRectangle(new SolidBrush(Video.NesColors[palette[i * 4 + 2]]), 32, 0, 16, 16);
                    g.FillRectangle(new SolidBrush(Video.NesColors[palette[i * 4 + 3]]), 48, 0, 16, 16);
                }
                subpalSprites.Images.Add(sb);
            }
        }

        private void setObjects()
        {
            byte bigBlockId = (Globals.gameType == GameType.CAD) ? (byte)Globals.levelData[curActiveLevel].bigBlockId : (byte)curActiveBigBlock;
            int addr = Globals.getTilesAddr(bigBlockId);
            for (int i = 0; i < Globals.OBJECTS_COUNT; i++)
            {
                byte c1 = Globals.romdata[addr + i];
                byte c2 = Globals.romdata[addr + 0x100 + i];
                byte c3 = Globals.romdata[addr + 0x200 + i];
                byte c4 = Globals.romdata[addr + 0x300 + i];
                byte typeColor = Globals.romdata[addr + 0x400 + i];
                objects[i] = new ObjRec(c1, c2, c3, c4, typeColor);
            }

            refillPanel();
        }

        private void setVideo()
        {
            byte backId = getBackId();
            for (int i = 0; i < 4; i++)
            {
                Bitmap b = Video.makeImageStrip(ConfigScript.getVideoChunk(backId), palette, i, 2, false);
                videoSprites[i].Images.Clear();
                videoSprites[i].Images.AddStrip(b);
            }
        }

        private void setVideoImage()
        {
            var b = new Bitmap(256, 256);
            using (Graphics g = Graphics.FromImage(b))
            {
                for (int i = 0; i < Globals.CHUNKS_COUNT; i++)
                {
                    int x = i % 16;
                    int y = i / 16;
                    g.DrawImage(videoSprites[curSubpalIndex].Images[i], new Rectangle(x * 16, y * 16, 16, 16));
                }
            }
            mapScreen.Image = b;
        }

        //cad specific
        private int curActiveLevel = 0;
        private int curDoor = 0;
        //generic
        private int curActiveVideo = 0x90;
        private int curActiveBigBlock = 0;
        private int curActivePal = 0;
        //editor
        private int curSubpalIndex = 0;
        private int curActiveBlock = 0;

        private byte[] palette = new byte[Globals.PAL_LEN];
        private ObjRec[] objects = new ObjRec[256];
        private ImageList[] videoSprites = new ImageList[4];
        private byte[] curActiveBack = new byte[16];
        private bool dirty;
        private bool readOnly;

        private string[] subPalItems = { "1", "2", "3", "4" };

        private void cbSubpalette_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (cbSubpalette.SelectedIndex == -1)
                return;
            curSubpalIndex = cbSubpalette.SelectedIndex;
            setVideoImage();
        }

        private void cbSubpalette_DrawItemEvent(object sender, DrawItemEventArgs e)
        {
            if (e.Index == -1)
            {
                e.DrawBackground();
                e.DrawFocusRectangle();
                return;
            }
            e.DrawBackground();
            e.Graphics.DrawImage(subpalSprites.Images[e.Index], e.Bounds.Width - 63, e.Bounds.Y, 64, 16);
            string text = cbSubpalette.Items[e.Index].ToString();
            e.Graphics.DrawString(text, cbSubpalette.Font,
                System.Drawing.Brushes.Black,
                new RectangleF(e.Bounds.X, e.Bounds.Y, e.Bounds.Width, e.Bounds.Height));
            e.DrawFocusRectangle();
        }

        void pb_MouseClick(object sender, MouseEventArgs e)
        {
            int x = e.X / 16;
            int y = e.Y / 16;
            PictureBox p = (PictureBox)sender;
            int objIndex = (int)p.Tag;
            if (x == 0)
            {
                if (y == 0)
                    objects[objIndex].c1 = (byte)curActiveBlock;
                else
                    objects[objIndex].c3 = (byte)curActiveBlock;
            }
            else
            {
                if (y == 0)
                    objects[objIndex].c2 = (byte)curActiveBlock;
                else
                    objects[objIndex].c4 = (byte)curActiveBlock;
            }
            p.Image = makeObjImage(objIndex);
            dirty = true;
        }

        void cbColor_SelectedIndexChanged(object sender, EventArgs e)
        {
            ComboBox cb = (ComboBox)sender;
            PictureBox pb = (PictureBox)cb.Tag;
            int index = (int)pb.Tag;
            objects[index].typeColor = (byte)(objects[index].typeColor & 0xF0 | cb.SelectedIndex);
            pb.Image = makeObjImage((int)pb.Tag);
            dirty = true;
        }

        void cbType_SelectedIndexChanged(object sender, EventArgs e)
        {
            ComboBox cb = (ComboBox)sender;
            int index = (int)cb.Tag;
            objects[index].typeColor = (byte)((objects[index].typeColor & 0x0F) | (cb.SelectedIndex << 4));
            //try
            int t = objects[index].getType();
            var pbBack = (PictureBox)mapObjects.Controls[index].Controls[4];
            pbBack.Visible = t >= 0x0E || t == 1;
            dirty = true;
        }

        public Image makeObjImage(int index)
        {
            if (Globals.gameType == GameType.DT2)
            {
                return makeObjImageDt2(index);
            }
            Bitmap b = new Bitmap(32, 32);
            var obj = objects[index];
            using (Graphics g = Graphics.FromImage(b))
            {
                g.DrawImage(videoSprites[obj.getSubpallete()].Images[obj.c1], new Rectangle(0, 0, 16, 16));
                g.DrawImage(videoSprites[obj.getSubpallete()].Images[obj.c2], new Rectangle(16, 0, 16, 16));
                g.DrawImage(videoSprites[obj.getSubpallete()].Images[obj.c3], new Rectangle(0, 16, 16, 16));
                g.DrawImage(videoSprites[obj.getSubpallete()].Images[obj.c4], new Rectangle(16, 16, 16, 16));
            }
            return b;
        }

        public Image makeObjImageDt2(int index)
        {
            Bitmap b = new Bitmap(32, 32);
            var obj = objects[index];
            var objectForColor = objects[index / 4];
            using (Graphics g = Graphics.FromImage(b))
            {
                g.DrawImage(videoSprites[objectForColor.getSubpalleteForDt2(index % 4)].Images[obj.c1], new Rectangle(0, 0, 16, 16));
                g.DrawImage(videoSprites[objectForColor.getSubpalleteForDt2(index % 4)].Images[obj.c2], new Rectangle(16, 0, 16, 16));
                g.DrawImage(videoSprites[objectForColor.getSubpalleteForDt2(index % 4)].Images[obj.c3], new Rectangle(0, 16, 16, 16));
                g.DrawImage(videoSprites[objectForColor.getSubpalleteForDt2(index % 4)].Images[obj.c4], new Rectangle(16, 16, 16, 16));
            }
            return b;
        }

        private void mapScreen_MouseClick(object sender, MouseEventArgs e)
        {
            int x = e.X / 16;
            int y = e.Y / 16;
            curActiveBlock = y * 16 + x;
            pbActive.Image = videoSprites[curSubpalIndex].Images[curActiveBlock];
            dirty = true;
        }

        private byte getBlockId()
        {
            byte blockId;
            if (Globals.gameType == GameType.CAD)
                blockId = (byte)Globals.levelData[curActiveLevel].bigBlockId;
            else
                blockId = (byte)curActiveBigBlock;
            return blockId;
        }

        private bool saveToFile()
        {
            byte blockId = getBlockId();
            int addr = Globals.getTilesAddr(blockId);
            for (int i = 0; i < Globals.OBJECTS_COUNT; i++)
            {
                Globals.romdata[addr + i] = objects[i].c1;
                Globals.romdata[addr + 0x100 + i] = objects[i].c2;
                Globals.romdata[addr + 0x200 + i] = objects[i].c3;
                Globals.romdata[addr + 0x300 + i] = objects[i].c4;
                Globals.romdata[addr + 0x400 + i] = objects[i].typeColor;
            }

            if (Globals.gameType == GameType.CAD)
            {
                int backAddr = Globals.getBackTileAddr(curActiveLevel);
                for (int i = 0; i < 16; i++)
                    Globals.romdata[backAddr + i] = curActiveBack[i];
            }

            dirty = !Globals.flushToFile();
            return !dirty;
        }

        private void btSave_Click(object sender, EventArgs e)
        {
            saveToFile();
        }

        private void cbLevelSelect_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (cbLevelSelect.SelectedIndex == -1 || cbTileset.SelectedIndex == -1)
                return;
            if (!readOnly && dirty)
            {
                DialogResult dr = MessageBox.Show("Tiles was changed. Do you want to save current tileset?", "Save", MessageBoxButtons.YesNoCancel);
                if (dr == DialogResult.Cancel)
                {
                    returnCbLevelIndexes();
                    return;
                }
                else if (dr == DialogResult.Yes)
                {
                    if (!saveToFile())
                    {
                        returnCbLevelIndexes();
                        return;
                    }
                }
                else
                {
                    dirty = false;
                }
            }
            curActiveLevel = cbLevelSelect.SelectedIndex;
            curActiveBigBlock = cbTileset.SelectedIndex;
            curActiveBlock = 0;
            curSubpalIndex = 0;
            reloadLevel();
        }

        private void BlockEdit_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (!readOnly && dirty)
            {
                DialogResult dr = MessageBox.Show("Tiles was changed. Do you want to save current tileset?", "Save", MessageBoxButtons.YesNo);
                if (dr == DialogResult.Yes)
                    saveToFile();
            }
        }


        private void returnCbLevelIndexes()
        {
            cbLevelSelect.SelectedIndexChanged -= cbLevelSelect_SelectedIndexChanged;
            cbLevelSelect.SelectedIndex = curActiveLevel;
            cbLevelSelect.SelectedIndexChanged += cbLevelSelect_SelectedIndexChanged;
            cbTileset.SelectedIndexChanged -= cbLevelSelect_SelectedIndexChanged;
            cbTileset.SelectedIndex = curActiveBigBlock;
            cbTileset.SelectedIndexChanged += cbLevelSelect_SelectedIndexChanged;
        }

        private void preparePanel()
        {
            //GUI
            mapObjects.Controls.Clear();
            mapObjects.SuspendLayout();
            var objTypesCad =
                new[]  {
                    "0 (back)",
                    "1 (collect)",
                    "2 (platform)",
                    "3 (block)",
                    "4 (spikes)",
                    "5 (door)",
                    "6 (mask)",
                    "7 (? block and go up)",
                    "8 (? block and go down)",
                    "9 (? block and go down)",
                    "A (Block)",
                    "B (Pit)",
                    "C (Block)",
                    "D (Block)",
                    "E (throwable stone)",
                    "F (throwable box)"
                };

            var objTypesDw =
                new[] {
                    "0 (back)",
                    "1 (hook)",
                    "2 (platform)",
                    "3 (block)",
                    "4 (spikes)",
                    "5 (door)",
                    "6",
                    "7",
                    "8",
                    "9",
                    "A",
                    "B",
                    "C",
                    "D",
                    "E",
                    "F"
                };

            var objTypesDt =
    new[] {
                    "0 (back)",
                    "1 (block)",
                    "2 ()",
                    "3 ()",
                    "4 ()",
                    "5 ()",
                    "6",
                    "7",
                    "8",
                    "9",
                    "A",
                    "B",
                    "C",
                    "D",
                    "E",
                    "F"
                };

            var objTypesDt2 =
                new[] {
                    "no",
                    "no",
                    "no",
                    "no",
                    "no",
                    "no",
                    "no",
                    "no",
                    "no",
                    "no",
                    "no",
                    "no",
                    "no",
                    "no",
                    "no",
                    "no"
                };
            for (int i = 0; i < Globals.OBJECTS_COUNT; i++)
            {
                Panel fp = new Panel();
                fp.Size = new Size(mapObjects.Width - 25, 32);
                //
                Label lb = new Label();
                lb.Location = new Point(0, 0);
                lb.Size = new Size(24, 32);
                lb.Tag = i;
                lb.Text = String.Format("{0:X}",i);
                fp.Controls.Add(lb);
                //
                PictureBox pb = new PictureBox();
                pb.Location = new Point(24, 0);
                pb.Size = new Size(32, 32);
                pb.Tag = i;
                pb.MouseClick += new MouseEventHandler(pb_MouseClick);
                fp.Controls.Add(pb);
                //
                ComboBox cbColor = new ComboBox();
                cbColor.Size = cbSubpalette.Size;
                cbColor.Location = new Point(60, 0);
                cbColor.Tag = pb;
                cbColor.DrawMode = DrawMode.OwnerDrawVariable;
                cbColor.DrawItem += new DrawItemEventHandler(cbSubpalette_DrawItemEvent);
                cbColor.Items.AddRange(subPalItems);
                cbColor.DropDownStyle = ComboBoxStyle.DropDownList;
                cbColor.SelectedIndexChanged += cbColor_SelectedIndexChanged;
                fp.Controls.Add(cbColor);
                //
                ComboBox cbType = new ComboBox();
                var objectTypes = Globals.gameType == GameType.Generic ? objTypesDw : (Globals.gameType == GameType.CAD) ? objTypesCad : (Globals.gameType == GameType.DT) ? objTypesDt : objTypesDt2;
                cbType.Items.AddRange(objectTypes);
                cbType.Location = new Point(156, 0);
                cbType.Size = new Size(120, 21);
                cbType.Tag = i;
                cbType.DropDownStyle = ComboBoxStyle.DropDownList;
                cbType.SelectedIndexChanged += cbType_SelectedIndexChanged;
                fp.Controls.Add(cbType);
                //
                PictureBox pb2 = new PictureBox();
                pb2.Location = new Point(280, 0);
                pb2.Size = new Size(32, 32);
                pb2.Tag = i;
                pb2.Visible = false;
                fp.Controls.Add(pb2);
                //
                mapObjects.Controls.Add(fp);
            }
            mapObjects.ResumeLayout();
        }

        private void refillPanel()
        {
            //GUI
            bool isCad = Globals.gameType == GameType.CAD;
            for (int i = 0; i < Globals.OBJECTS_COUNT; i++)
            {
                Panel p = (Panel)mapObjects.Controls[i];
                PictureBox pb = (PictureBox)p.Controls[1];
                pb.Image = makeObjImage(i);
                ComboBox cbColor = (ComboBox)p.Controls[2];
                cbColor.SelectedIndex = objects[i].getSubpallete();
                ComboBox cbType = (ComboBox)p.Controls[3];
                cbType.SelectedIndex = objects[i].getType();

                PictureBox pb2 = (PictureBox)p.Controls[4];
                pb2.Image = makeObjImage(curActiveBack[i % 16]);
                int t = objects[i].getType();
                pb2.Visible = isCad && (t >= 0x0E || t == 1);
            }
        }

        private void VisibleOnlyChange_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (cbDoor.SelectedIndex == -1 || cbVideo.SelectedIndex == -1 || cbPalette.SelectedIndex == -1)
                return;
            curDoor = cbDoor.SelectedIndex;
            curActiveVideo = cbVideo.SelectedIndex + 0x90;
            curActivePal = cbPalette.SelectedIndex;
            reloadLevel(false);
        }

        private void pbBacks_Paint(object sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            for (int i = 0; i < 16; i++)
                g.DrawImage(makeObjImage(curActiveBack[i]), new Point(i % 8 * 32 + i % 8, i / 8 * 32));
        }

        private void button2_Click(object sender, EventArgs e)
        {
            var f = new BoxBackForm();
            f.setParentForm(this);
            f.ShowDialog();
            reloadLevel(false);
        }

        public int getActiveLevel()
        {
            return curActiveLevel;
        }

        private void updatePanelsVisible()
        {
            bool generic = Globals.gameType != GameType.CAD;
            pnCad.Visible = !generic;
            pnGeneric.Visible = generic;
            pnBacks.Visible = !generic;
        }

        private void btClear_Click(object sender, EventArgs e)
        {
            if (MessageBox.Show("Are you sure want to clear all blocks?", "Clear", MessageBoxButtons.YesNo)!= DialogResult.Yes)
              return;
            for (int i = 0; i < Globals.OBJECTS_COUNT; i++)
                objects[i] = new ObjRec(0,0,0,0,0);
            dirty = true;
            refillPanel();
        }

        private byte getBackId()
        {
            byte backId;
            if (Globals.gameType == GameType.CAD)
            {
                if (curDoor <= 0)
                    backId = (byte)Globals.levelData[curActiveLevel].backId;
                else
                    backId = (byte)Globals.doorsData[curDoor - 1].backId;
            }
            else
            {
                backId = (byte)curActiveVideo;
            }
            return backId;
        }

        private void btFlipHorizontal_Click(object sender, EventArgs e)
        {
            var videoChunk = ConfigScript.getVideoChunk(getBackId());
            int beginIndex = 16 * curActiveBlock;
            for (int line = 0; line < 8; line++)
            {
                videoChunk[beginIndex + line    ] = Utils.ReverseBits(videoChunk[beginIndex + line]);
                videoChunk[beginIndex + line + 8] = Utils.ReverseBits(videoChunk[beginIndex + line + 8]); 
            }
            ConfigScript.setVideoChunk(getBackId(), videoChunk);
            reloadLevel(false);
            dirty = true;
        }

        private void btFlipVertical_Click(object sender, EventArgs e)
        {
            var videoChunk = ConfigScript.getVideoChunk(getBackId());
            int beginIndex = 16 * curActiveBlock;
            Utils.Swap(ref videoChunk[beginIndex + 0], ref videoChunk[beginIndex + 7]);
            Utils.Swap(ref videoChunk[beginIndex + 1], ref videoChunk[beginIndex + 6]);
            Utils.Swap(ref videoChunk[beginIndex + 2], ref videoChunk[beginIndex + 5]);
            Utils.Swap(ref videoChunk[beginIndex + 3], ref videoChunk[beginIndex + 4]);

            Utils.Swap(ref videoChunk[beginIndex + 8], ref videoChunk[beginIndex +15]);
            Utils.Swap(ref videoChunk[beginIndex + 9], ref videoChunk[beginIndex +14]);
            Utils.Swap(ref videoChunk[beginIndex +10], ref videoChunk[beginIndex +13]);
            Utils.Swap(ref videoChunk[beginIndex +11], ref videoChunk[beginIndex +12]);
            ConfigScript.setVideoChunk(getBackId(), videoChunk);
            reloadLevel(false);
            dirty = true;
        }

        private void btExport_Click(object sender, EventArgs e)
        {
            var f = new SelectFile();
            f.Filename = "exportedBlocks.bin";
            f.ShowDialog();
            if (!f.Result)
                return;
            var fn = f.Filename;
            byte blockId = getBlockId();
            var data = new byte[Globals.OBJECTS_COUNT * 5];
            for (int i = 0; i < Globals.OBJECTS_COUNT; i++)
            {
                data[i] = objects[i].c1;
                data[Globals.OBJECTS_COUNT * 1 + i] = objects[i].c2;
                data[Globals.OBJECTS_COUNT * 2 + i] = objects[i].c3;
                data[Globals.OBJECTS_COUNT * 3 + i] = objects[i].c4;
                data[Globals.OBJECTS_COUNT * 4 + i] = objects[i].typeColor;
            }

            Utils.saveDataToFile(fn, data);
        }

        private void btImport_Click(object sender, EventArgs e)
        {
            var f = new SelectFile();
            f.Filename = "exportedBlocks.bin";
            f.ShowDialog();
            if (!f.Result)
                return;
            var fn = f.Filename;
            var data = Utils.loadDataFromFile(fn);
            if (data == null)
                return;

            byte bigBlockId = (Globals.gameType == GameType.CAD) ? (byte)Globals.levelData[curActiveLevel].bigBlockId : (byte)curActiveBigBlock;
            int addr = Globals.getTilesAddr(bigBlockId);
            for (int i = 0; i < Globals.OBJECTS_COUNT; i++)
            {
                Globals.romdata[addr + i] = data[i];
                Globals.romdata[addr + 0x100 + i] = data[i + 0x100];
                Globals.romdata[addr + 0x200 + i] = data[i + 0x200];
                Globals.romdata[addr + 0x300 + i] = data[i + 0x300];
                Globals.romdata[addr + 0x400 + i] = data[i + 0x400];
            }
            reloadLevel(false);
            dirty = true;
        }
    }
}
