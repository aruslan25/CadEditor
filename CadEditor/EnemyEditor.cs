﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using System.IO;

namespace CadEditor
{
    public partial class EnemyEditor : Form
    {
        public EnemyEditor()
        {
            InitializeComponent();
        }

        private int curActiveLevel = 0;
        private int curActiveBlock = 0;
        private int curActiveScreen = 0;

        private int curActiveLayout = 0;
        private int curVideoNo = 0x90;
        private int curBigBlockNo = 0;
        private int curBlockNo = 0;
        private int curPaletteNo = 0;
        private int curWidth = 1;
        private int curHeight = 1;

        private LevelLayerData curLevelLayerData = new LevelLayerData();
        private Image[] scrImages;
        private List<ObjectRec> objects = new List<ObjectRec>();
        private bool dirty = false;
        private bool readOnly = false;

        const int MAX_SIZE = 64;
        const int OBJECTS_COUNT = 256;

        private void reloadLevelLayerData(bool resetScreenPos)
        {
            if (Globals.gameType == GameType.CAD)
            {
                var lr = Globals.levelData[curActiveLevel];
                int layoutAddr = lr.getActualLayoutAddr();
                int scrollAddr = lr.getActualScrollAddr();
                int dirAddr = lr.getActualDirsAddr();
                int width = lr.getWidth();
                int height = lr.getHeight();
                byte[] layer = new byte[width * height];
                byte[] scroll = new byte[width * height];
                byte[] dirs = new byte[height];
                for (int i = 0; i < width * height; i++)
                {
                    layer[i] = Globals.romdata[layoutAddr + i];
                    scroll[i] = Globals.romdata[scrollAddr + i];
                }
                for (int i = 0; i < height; i++)
                {
                    dirs[i] = Globals.romdata[dirAddr + i];
                }
                curLevelLayerData = new LevelLayerData(width, height, layer, scroll, dirs);
            }
            else
            {
                curWidth = Globals.getLevelWidth(curActiveLayout);
                curHeight = Globals.getLevelHeight(curActiveLayout);
                curActiveLayout = cbLayoutNo.SelectedIndex;
                curVideoNo = cbVideoNo.SelectedIndex + 0x90;
                curBigBlockNo = cbBigBlockNo.SelectedIndex;
                curBlockNo = cbBlockNo.SelectedIndex;
                curPaletteNo = cbPaletteNo.SelectedIndex;

                int layoutAddr = Globals.getLayoutAddr(curActiveLayout);
                int width = curWidth;
                int height = curHeight;
                byte[] layer = new byte[width * height];
                for (int i = 0; i < width * height; i++)
                    layer[i] = Globals.romdata[layoutAddr + i];
                curLevelLayerData = new LevelLayerData(width, height, layer, null, null);
            }

            if (resetScreenPos)
            {
                cbScreenNo.Items.Clear();
                for (int i = 0; i < curLevelLayerData.width * curLevelLayerData.height; i++)
                    cbScreenNo.Items.Add(String.Format("{0:X}", i));
                cbScreenNo.SelectedIndex = findStartPosition();
            }
        }

        //for CAD editor only
        private void makeScreens()
        {
            //!!!duplicate in EditLayout.cs
            scrImages = new Image[ConfigScript.screensOffset.recCount];
            for (int i = 0; i < ConfigScript.screensOffset.recCount; i++)
                scrImages[i] = Video.emptyScreen(512, 512);

            bool stopOnDoors = cbStopOnDoors.Checked;
            var screenList = Globals.buildScreenRecs(curActiveLevel, stopOnDoors);

            var sortedScreenList = new List<ScreenRec>(screenList);
            sortedScreenList.Sort((r1, r2) => { return r1.door > r2.door ? 1 : r1.door < r2.door ? -1 : 0; });
            int lastDoorNo = -1;
            int levelNo = curActiveLevel;

            byte blockId = (byte)Globals.levelData[curActiveLevel].bigBlockId;
            byte backId = 0, palId = 0;
            for (int i = 0; i < sortedScreenList.Count; i++)
            {
                if (lastDoorNo != sortedScreenList[i].door)
                {
                    lastDoorNo = sortedScreenList[i].door;
                    if (lastDoorNo == 0)
                    {
                        backId = (byte)Globals.levelData[curActiveLevel].backId;
                        palId = (byte)Globals.levelData[curActiveLevel].palId;
                    }
                    else
                    {
                        backId = (byte)Globals.doorsData[lastDoorNo - 1].backId;
                        palId = (byte)Globals.doorsData[lastDoorNo - 1].palId;
                    }
                }
                int scrNo = sortedScreenList[i].no;
                int addEH = (curActiveLevel == 5 || curActiveLevel==8) ? 256 : 0;
                int realScrNo = scrNo - 1 + addEH;
                scrImages[scrNo] = Video.makeScreen(realScrNo, backId, blockId, blockId, palId);
            }
        }

        private int findStartPosition()
        {
            int w = curLevelLayerData.width;
            int h = curLevelLayerData.height;
            return w * (h - 1);
        }

        //for generic editor
        private Bitmap makeCurScreen(int scrNo)
        {
            return  Video.makeScreen(scrNo, curVideoNo, curBigBlockNo, curBlockNo, curPaletteNo);
        }

        private void setBackImage()
        {
            int scrNo = curLevelLayerData.layer[curActiveScreen];
            if (Globals.gameType != GameType.CAD && cbPlus256.Checked)
                scrNo += 256;
            lbScrNo.Text = String.Format("({0:X})", scrNo);
            mapScreen.Image = (Globals.gameType != GameType.CAD) ? makeCurScreen(scrNo-1) :  scrImages[scrNo];
        }

        private void reloadLevel(bool reloadObjects)
        {
            if (Globals.gameType == GameType.CAD)
                makeScreens();
            reloadLevelLayerData(reloadObjects);
            setBackImage();
            if (reloadObjects)
              setObjects();
            curActiveBlock = 0;
            btDelete.Enabled = false;
        }

        private void cbLevel_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (cbLevel.SelectedIndex == -1 || cbLayoutNo.SelectedIndex == -1)
                return;
            bool realReload = (sender == cbLevel) || (sender == cbLayoutNo);
            bool reloadObjects = realReload;
            if (!readOnly && realReload)
            {
                if (!Utils.askToSave(ref dirty, saveToFile, returnCbLevelIndex))
                    return;
            }
           
            curActiveLevel = cbLevel.SelectedIndex;
            curActiveLayout = cbLayoutNo.SelectedIndex;
            cbLayoutNo.Items.Clear();
            foreach (var lr in ConfigScript.levelRecs)
                cbLayoutNo.Items.Add(String.Format("0x{0:X} ({1}x{2})", lr.layoutAddr, lr.width, lr.height));
            Utils.setCbIndexWithoutUpdateLevel(cbLayoutNo, cbLevel_SelectedIndexChanged, curActiveLayout);
            reloadLevel(reloadObjects);
        }

        private void EnemyEditor_Load(object sender, EventArgs e)
        {
            reloadPictures();
            fillObjPanel();

            cbCoordX.Items.Clear();
            cbCoordY.Items.Clear();
            for (int i = 0; i < 256; i++)
            {
                cbCoordX.Items.Add(String.Format("{0:X}", i));
                cbCoordY.Items.Add(String.Format("{0:X}", i));
            }

            Utils.setCbItemsCount(cbVideoNo, ConfigScript.videoOffset.recCount);
            Utils.setCbItemsCount(cbBigBlockNo, ConfigScript.bigBlocksOffset.recCount);
            Utils.setCbItemsCount(cbBlockNo, ConfigScript.blocksOffset.recCount);
            Utils.setCbItemsCount(cbPaletteNo, ConfigScript.palOffset.recCount);
            Utils.setCbIndexWithoutUpdateLevel(cbVideoNo, cbLevel_SelectedIndexChanged);
            Utils.setCbIndexWithoutUpdateLevel(cbBlockNo, cbLevel_SelectedIndexChanged);
            Utils.setCbIndexWithoutUpdateLevel(cbBigBlockNo, cbLevel_SelectedIndexChanged);
            Utils.setCbIndexWithoutUpdateLevel(cbPaletteNo, cbLevel_SelectedIndexChanged);
            cbLayoutNo.Items.Clear();
            foreach (var lr in ConfigScript.levelRecs)
                cbLayoutNo.Items.Add(String.Format("0x{0:X} ({1}x{2})", lr.layoutAddr, lr.width, lr.height));
            Utils.setCbIndexWithoutUpdateLevel(cbLayoutNo, cbLevel_SelectedIndexChanged);
            cbLevel.SelectedIndex = 0;
            updatePanelsVisibility();

            readOnly = Globals.gameType == GameType.DT2;
            btSave.Enabled = !readOnly;
            lbReadOnly.Visible = readOnly;

        }

        private void cbScreenNo_SelectedIndexChanged(object sender, EventArgs e)
        {
            curActiveScreen = cbScreenNo.SelectedIndex;
            int w = curLevelLayerData.width;
            int h = curLevelLayerData.height;
            btLeft.Enabled = curActiveScreen % w != 0;
            btRight.Enabled = curActiveScreen % w != w - 1;
            btUp.Enabled = curActiveScreen >= w;
            btDown.Enabled = curActiveScreen < w * (h - 1);
            setBackImage();
            mapScreen.Invalidate();
        }

        private void fillObjPanel()
        {
            objPanel.Controls.Clear();
            for (int i = 0; i < objectSprites.Images.Count; i++)
            {
                var but = new Button();
                but.Size = new Size(32, 32);
                but.ImageList = objectSprites;
                but.ImageIndex = i;
                but.Click += new EventHandler(buttonObjClick);
                objPanel.Controls.Add(but);
            }
        }

        private void buttonObjClick(Object button, EventArgs e)
        {
            int index = ((Button)button).ImageIndex;
            activeBlock.Image = objectSprites.Images[index];
            lbActive.Text = String.Format("({0:X2})", index);
            curActiveBlock = index;
        }

        private void sortObjects()
        {
            List<ObjectRec> sortedObjects = new List<ObjectRec>();
            bool stopOnDoor = cbStopOnDoors.Checked;
            List<ScreenRec> sortedScreens = Globals.buildScreenRecs(curActiveLevel, stopOnDoor);
            for (int i = 0; i < sortedScreens.Count; i++)
            {
                var scrrec = sortedScreens[i];
                var objectsAtScreen = objects.FindAll((o) => { return (o.sx == scrrec.sx) && (o.sy == scrrec.sy); });
                int backSortCoeff = scrrec.backSort ? -1 : 1;
                if (scrrec.upsort)
                    objectsAtScreen.Sort((obj1, obj2) => { return obj1.y < obj2.y ? 1 : obj1.y > obj2.y ? -1 : 0; });
                else
                    objectsAtScreen.Sort((obj1, obj2) => { return obj1.x > obj2.x ? backSortCoeff : obj1.x < obj2.x ? -backSortCoeff : 0; });
                sortedObjects.AddRange(objectsAtScreen);
            }
            var undefinedObjects = objects.FindAll(isUndefindedObj);
            sortedObjects.AddRange(undefinedObjects);
            var ffObjects = objects.FindAll((o) => { return o.type == 0xFF; });
            sortedObjects.AddRange(ffObjects);
            if (sortedObjects.Count != objects.Count)
            {
                MessageBox.Show("Error while sorting objects. Some objects no parent screen. View object list and delete dead objects");
            }
            objects = sortedObjects;
            fillObjectsListBox();
        }

        private bool isUndefindedObj(ObjectRec obj)
        {
            int scrNo = coordToScreenNo(obj);
            if (scrNo == -1)
                return false;
           
           return (obj.type != 0xFF) && (curLevelLayerData.layer[scrNo] == 0);
        }

        private void deleteSelected()
        {
            var toRemove = new List<ObjectRec>();
            for (int i = 0; i < lvObjects.SelectedIndices.Count; i++)
            {
                int index = lvObjects.SelectedIndices[i];
                if (index == -1)
                    continue;
                toRemove.Add(objects[index]);
            }
            for (int i = 0; i < toRemove.Count; i++)
                objects.Remove(toRemove[i]);
            fillObjectsListBox();

            btDelete.Enabled = false;
            mapScreen.Invalidate();
            dirty = true;
        }

        private void lbObjects_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Delete)
            {
                deleteSelected();
            }
        }

        private LevelRec getLevelRecForGameType()
        {
            return ConfigScript.getLevelRec(curActiveLayout);
        }

        private void setObjectsDt()
        {
            objects.Clear();
            var lr = getLevelRecForGameType();
            int objCount = lr.objCount;
            int addr = lr.objectsBeginAddr;
            var objLineOffsets = new byte[lr.height];
            for (int i = 0; i < lr.height; i++)
            {
                objLineOffsets[i] = Globals.romdata[addr + i - lr.height];
            }
            for (int i = 0; i < objCount; i++)
            {
                byte v = Globals.romdata[addr + i];
                byte sx = Globals.romdata[addr - 3 * objCount + i - lr.height];
                byte x = Globals.romdata[addr - 2 * objCount + i - lr.height];
                byte y = Globals.romdata[addr - objCount + i - lr.height];
                byte sy = convertObjectIndexToScreenYcoord(objLineOffsets, i);
                var obj = new ObjectRec(v, sx, sy, x, y);
                objects.Add(obj);
            }
        }

        private void setObjectsCadDwd()
        {
            objects.Clear();
            int objCount, addr;
            LevelRec lr;
            if (Globals.gameType == GameType.CAD)
            {
                lr = ConfigScript.getLevelRec(curActiveLevel);
                objCount = lr.objCount;
                addr = lr.objectsBeginAddr;
            }
            else
            {
                lr = getLevelRecForGameType();
                objCount = lr.objCount;
                addr = lr.objectsBeginAddr;
            }

            for (int i = 0; i < objCount; i++)
            {
                byte v = Globals.romdata[addr + i];
                if ((curActiveLevel != 4) || (Globals.gameType == GameType.Generic))
                {
                    byte sx, sy, x, y;
                    if (!ConfigScript.isDwdAdvanceLastLevel())
                    {
                        sx = Globals.romdata[addr - 4 * objCount + i];
                        x = Globals.romdata[addr - 3 * objCount + i];
                        sy = Globals.romdata[addr - 2 * objCount + i];
                        y = Globals.romdata[addr - objCount + i];
                    }
                    else
                    {
                        sx = Globals.romdata[addr + 1 * objCount + i];
                        x = Globals.romdata[addr + 2 * objCount + i];
                        sy = Globals.romdata[addr + 3 * objCount + i];
                        y = Globals.romdata[addr - +4 * objCount + i];
                    }
                    var obj = new ObjectRec(v, sx, sy, x, y);
                    objects.Add(obj);
                }
                else  //C&D LEVEL D EXCEPTION
                {
                    byte sx = Globals.romdata[addr - 4 * objCount + 1 + i];
                    byte x = Globals.romdata[addr - 3 * objCount + 1 + i];
                    byte sy = Globals.romdata[addr - 2 * objCount + 1 + i];
                    byte y = Globals.romdata[addr - objCount + i];
                    var obj = new ObjectRec(v, sx, sy, x, y);
                    objects.Add(obj);
                }
            }
        }

        private void setObjectsDt2()
        {
            objects.Clear();
            var lr = getLevelRecForGameType();
            int objCount = lr.objCount;
            int addr = lr.objectsBeginAddr;

            int objectsReaded = 0;
            int currentHeight = 0;
            while (objectsReaded < objCount)
            {
                byte command = Globals.romdata[addr];
                if (command == 0xFF)
                {
                    currentHeight = Globals.romdata[addr + 1];
                    if (currentHeight == 0xFF)
                        break;
                    addr += 2;
                }
                else
                {
                    byte v = Globals.romdata[addr + 2];
                    byte xbyte = Globals.romdata[addr + 0];
                    byte ybyte = Globals.romdata[addr + 1];
                    byte sx = (byte)(xbyte >> 5);
                    byte x = (byte)((xbyte & 0x1F) << 3);
                    byte sy = (byte)currentHeight;
                    byte y = ybyte;
                    var obj = new ObjectRec(v, sx, sy, x, y);
                    objects.Add(obj);
                    objectsReaded++;
                    addr += 3;
                }
            }
        }

        private void setObjects()
        {
            if (Globals.gameType == GameType.DT)
            {
                setObjectsDt();
            }
            else if (Globals.gameType == GameType.DT2)
            {
                setObjectsDt2();
            }
            else
            {
                setObjectsCadDwd();
            }
            
            fillObjectsListBox();
        }

        //duck tales specific function - convert object index to screen Y coord
        private byte convertObjectIndexToScreenYcoord(byte[] objLineOffsets, int index)
        {
            for (int i = 1; i < objLineOffsets.Length; i++)
                if (index < objLineOffsets[i])
                    return (byte)(i-1);
            return (byte)(objLineOffsets.Length - 1);
        }

        private void fillObjectsListBox()
        {
            lvObjects.Items.Clear();
            for (int i = 0; i < objects.Count; i++)
                lvObjects.Items.Add(new ListViewItem(makeStringForObject(objects[i]), objects[i].type));
            cbCoordX.Enabled = false;
            cbCoordY.Enabled = false;
        }

        private int coordToScreenNo(ObjectRec obj)
        {
            int index = obj.sy * curLevelLayerData.width + obj.sx;
            if (index < 0 || index >= curLevelLayerData.width * curLevelLayerData.height)
                return -1;
            return index;
        }

        private String makeStringForObject(ObjectRec obj)
        {
            int screenNo = coordToScreenNo(obj);
            String ans = String.Format("{0} <{1:X}>", obj.ToString(), screenNo);
            return ans;
        }

        private void btClearObjs_Click(object sender, EventArgs e)
        {
            List<ObjectRec> toRemove = new List<ObjectRec>();
            for (int i = 0; i < objects.Count; i++)
            {
                int screenNo = coordToScreenNo(objects[i]);
                if (screenNo == curActiveScreen)
                    toRemove.Add(objects[i]);
            }
            for (int i = 0; i < toRemove.Count; i++)
                objects.Remove(toRemove[i]);

            fillObjectsListBox();
            mapScreen.Invalidate();
            dirty = true;
        }

        private void btLeft_Click(object sender, EventArgs e)
        {
            cbScreenNo.SelectedIndex = --curActiveScreen; ;
        }

        private void btRight_Click(object sender, EventArgs e)
        {
            cbScreenNo.SelectedIndex = ++curActiveScreen;
        }

        private void btUp_Click(object sender, EventArgs e)
        {
            curActiveScreen -= curLevelLayerData.width;
            cbScreenNo.SelectedIndex = curActiveScreen;
        }

        private void btDown_Click(object sender, EventArgs e)
        {
            curActiveScreen += curLevelLayerData.width;
            cbScreenNo.SelectedIndex = curActiveScreen;
        }

        private Point screenNoToCoord()
        {
            int width = curLevelLayerData.width;
            int height = curLevelLayerData.height;
            return new Point(curActiveScreen % width, curActiveScreen / width);
        }

        private void mapScreen_MouseClick(object sender, MouseEventArgs e)
        {
            int dx = e.X / 64;
            int dy = e.Y / 64;
            //int index = dy * 8 + dx;
            dirty = true;
            Point coord = screenNoToCoord();
            byte type = (byte)curActiveBlock;
            byte sx = (byte)coord.X, sy = (byte)coord.Y;
            byte x = (byte)(e.X / 2);
            byte y = (byte)(e.Y / 2);
            var obj = new ObjectRec(type, sx, sy, x, y);
            objects.Add(obj);

            String ans = String.Format("{0} <{1}>", obj.ToString(), curActiveScreen);
            lvObjects.Items.Add(new ListViewItem(ans, obj.type));
            mapScreen.Invalidate();
        }

        private void mapScreen_Paint(object sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            for (int i = 0; i < objects.Count; i++)
            {
                var curObject = objects[i];
                int screenIndex = coordToScreenNo(curObject);
                if (screenIndex == curActiveScreen)
                {
                    int x = curObject.x, y = curObject.y;
                    if (curObject.type < objectSprites.Images.Count)
                        g.DrawImage(objectSprites.Images[curObject.type], new Point(x * 2 - 8, y * 2 - 8));
                }
            }
        }

        private void cbCoordX_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (lvObjects.SelectedItems.Count != 1)
                return;
            int index = lvObjects.SelectedItems[0].Index;
            var obj = objects[index];
            obj.x = (byte)cbCoordX.SelectedIndex;
            objects[index] = obj;
            lvObjects.SelectedItems[0].Text = makeStringForObject(obj);
            mapScreen.Invalidate();
        }

        private void cbCoordY_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (lvObjects.SelectedItems.Count != 1)
                return;
            int index = lvObjects.SelectedItems[0].Index;
            var obj = objects[index];
            obj.y = (byte)cbCoordY.SelectedIndex;
            objects[index] = obj;
            lvObjects.SelectedItems[0].Text = makeStringForObject(obj);
            mapScreen.Invalidate();
        }

        private bool saveToFile()
        {
            //todo : add save for duck tales 2

            var romFname = OpenFile.FileName;
            LevelRec lr = getLevelRecForGameType();
            //write objects
            if (!cbManualSort.Checked)
              sortObjects();

            int addrBase = lr.objectsBeginAddr;
            int objCount = lr.objCount;
             
            if (objects.Count > objCount)
            {
                MessageBox.Show(String.Format("Too many objects in level ({0}). Maximum: {1}", objects.Count, lr.objCount));
                return false;
            }
            //level D hack (for C&D)
            int levelDhack = (curActiveLevel == 4 && Globals.gameType == GameType.CAD) ? 1 : 0;
            try
            {
                if (Globals.gameType != GameType.DT)
                {
                    for (int i = 0; i < objects.Count; i++)
                    {
                        var obj = objects[i];
                        if (!ConfigScript.isDwdAdvanceLastLevel())
                        {
                            Globals.romdata[addrBase + i] = obj.type;
                            Globals.romdata[addrBase - 4 * objCount + levelDhack + i] = obj.sx;
                            Globals.romdata[addrBase - 3 * objCount + levelDhack + i] = obj.x;
                            Globals.romdata[addrBase - 2 * objCount + levelDhack + i] = obj.sy;
                            Globals.romdata[addrBase - objCount + i] = obj.y;
                        }
                        else
                        {
                            Globals.romdata[addrBase + i] = obj.type;
                            Globals.romdata[addrBase + 1 * objCount + levelDhack + i] = obj.sx;
                            Globals.romdata[addrBase + 2 * objCount + levelDhack + i] = obj.x;
                            Globals.romdata[addrBase + 3 * objCount + levelDhack + i] = obj.sy;
                            Globals.romdata[addrBase + 4 * objCount + i] = obj.y;
                        }

                    }
                    for (int i = objects.Count; i < objCount; i++)
                    {
                        Globals.romdata[addrBase + i] = 0xFF;
                        if (!ConfigScript.isDwdAdvanceLastLevel())
                        {
                            Globals.romdata[addrBase - 4 * objCount + levelDhack + i] = 0xFF;
                            Globals.romdata[addrBase - 3 * objCount + levelDhack + i] = 0xFF;
                            Globals.romdata[addrBase - 2 * objCount + levelDhack + i] = 0xFF;
                            Globals.romdata[addrBase - objCount + levelDhack + i] = 0xFF;
                        }
                        else
                        {
                            Globals.romdata[addrBase + 1 * objCount + levelDhack + i] = 0xFF;
                            Globals.romdata[addrBase + 2 * objCount + levelDhack + i] = 0xFF;
                            Globals.romdata[addrBase + 3 * objCount + levelDhack + i] = 0xFF;
                            Globals.romdata[addrBase + 4 * objCount + levelDhack + i] = 0xFF;
                        }
                    }
                }
                else
                {
                    for (int i = 0; i < objects.Count; i++)
                    {
                        var obj = objects[i];
                        Globals.romdata[addrBase + i] = obj.type;
                        Globals.romdata[addrBase - 3 * objCount + i - lr.height] = obj.sx;
                        Globals.romdata[addrBase - 2 * objCount + i - lr.height] = obj.x;
                        Globals.romdata[addrBase - 1 * objCount + i - lr.height] = obj.y;
                    }
                    for (int i = objects.Count; i < objCount; i++)
                    {
                        Globals.romdata[addrBase + i] = 0xFF;
                        Globals.romdata[addrBase - 3 * objCount + i - lr.height] = 0xFF;
                        Globals.romdata[addrBase - 2 * objCount + i - lr.height] = 0xFF;
                        Globals.romdata[addrBase - 1 * objCount + i - lr.height] = 0xFF;
                    }
                }
            }
            catch (IndexOutOfRangeException ex)
            {
                MessageBox.Show(ex.Message, "Save error");
                return false;
            }

            dirty = !Globals.flushToFile();
            return !dirty;
        }

        private void lvObjects_SelectedIndexChanged(object sender, EventArgs e)
        {
            bool selectedZero = lvObjects.SelectedItems.Count == 0;
            bool selectedOne =  lvObjects.SelectedItems.Count == 1;
            bool selectedMany = lvObjects.SelectedItems.Count > 1;
            btDelete.Enabled = !selectedZero;
            cbCoordX.Enabled = selectedOne;
            cbCoordY.Enabled = selectedOne;
            btSortDown.Enabled = false;
            btSortUp.Enabled = false;

            if (selectedOne)
            {
                int index = lvObjects.SelectedItems[0].Index;
                cbCoordX.SelectedIndex = objects[index].x;
                cbCoordY.SelectedIndex = objects[index].y;
            }
            if (!selectedZero)
            {
                btSortDown.Enabled = cbManualSort.Checked && lvObjects.SelectedIndices[lvObjects.SelectedIndices.Count - 1] < objects.Count - 1;
                btSortUp.Enabled = cbManualSort.Checked && lvObjects.SelectedIndices[0] > 0;
            }
        }

        private void btSave_Click(object sender, EventArgs e)
        {
            saveToFile();
        }

        private void EnemyEditor_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (!readOnly && dirty)
            {
                DialogResult dr = MessageBox.Show("Level was changed. Do you want to save current level?", "Save", MessageBoxButtons.YesNo);
                if (dr == DialogResult.Yes)
                    saveToFile();
            }
        }

        private void returnCbLevelIndex()
        {
            Utils.setCbIndexWithoutUpdateLevel(cbLevel, cbLevel_SelectedIndexChanged, curActiveLevel);
            Utils.setCbIndexWithoutUpdateLevel(cbLayoutNo, cbLevel_SelectedIndexChanged, curActiveLayout);
            //save width/height
        }

        private void btSortUp_Click(object sender, EventArgs e)
        {
            var selInds = new List<int>();
            for (int i = 0; i < lvObjects.SelectedIndices.Count; i++)
            {
                int ind = lvObjects.SelectedIndices[i];
                selInds.Add(ind);
                var xchg = objects[ind];
                objects[ind] = objects[ind - 1];
                objects[ind - 1] = xchg;
            }
            fillObjectsListBox();
            for (int i = 0; i < selInds.Count; i++)
                lvObjects.Items[selInds[i] - 1].Selected = true;
            lvObjects.Select();
            dirty = true;
        }

        private void btSortDown_Click(object sender, EventArgs e)
        {
            var selInds = new List<int>();
            for (int i = lvObjects.SelectedIndices.Count-1; i >=0 ; i--)
            {
                int ind = lvObjects.SelectedIndices[i];
                selInds.Add(ind);
                var xchg = objects[ind];
                objects[ind] = objects[ind + 1];
                objects[ind + 1] = xchg;
            }
            fillObjectsListBox();
            for (int i = 0; i < selInds.Count; i++)
                lvObjects.Items[selInds[i] + 1].Selected = true;
            lvObjects.Select();
            dirty = true;
        }

        private void cbManualSort_CheckedChanged(object sender, EventArgs e)
        {
            if (lvObjects.SelectedIndices.Count > 0)
            {
                btSortDown.Enabled = cbManualSort.Checked && lvObjects.SelectedIndices[lvObjects.SelectedIndices.Count - 1] < objects.Count - 1;
                btSortUp.Enabled = cbManualSort.Checked && lvObjects.SelectedIndices[0] > 0;
            }
        }

        private void btDelete_Click(object sender, EventArgs e)
        {
            deleteSelected();
        }

        private void reloadPictures()
        {
            var objSpritesDir = Globals.gameType == GameType.Generic ? "obj_sprites_dwd" : (GameType.CAD == Globals.gameType) ? "obj_sprites_cad" : "obj_sprites";
            var objSpritesDirGeneric = "obj_sprites";
            var templ = objSpritesDir + "\\object{0}.png";
            var templGeneric = objSpritesDirGeneric + "\\object{0}.png";
            objectSprites.Images.Clear();
            for (int i = 0; i < OBJECTS_COUNT; i++)
            {
                var fname = String.Format(templ, i);
                var fnameGeneric = String.Format(templGeneric,i);
                if (File.Exists(fname))
                    objectSprites.Images.Add(Image.FromFile(fname));
                else if (File.Exists(fnameGeneric))
                    objectSprites.Images.Add(Image.FromFile(fnameGeneric));
            }
        }

        private void updatePanelsVisibility()
        {
            bool generic = Globals.gameType != GameType.CAD;
            pnCad.Visible = !generic;
            pnGeneric.Visible = generic;
            cbStopOnDoors.Visible = !generic;
            cbManualSort.Visible = !generic;
        }
    }

    struct ObjectRec
    {
        public ObjectRec(byte type, byte sx, byte sy, byte x, byte y)
        {
            this.type = type;
            this.sx = sx;
            this.sy = sy;
            this.x = x;
            this.y = y;
        }
        public byte type;
        public byte x;
        public byte y;
        public byte sx;
        public byte sy;

        public override String ToString()
        {
            String formatStr = (type > 15) ? "{0:X} : ({1:X}:{2:X})" : "0{0:X} : ({1:X}:{2:X})";
            return String.Format(formatStr, type, sx << 8 | x, sy << 8 | y);
        }
    }
}
