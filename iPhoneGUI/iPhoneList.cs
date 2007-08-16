// iPhoneList - an iPhone File Explorer for Windows
//
// Copyright (C) 2007  James C. Baker
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with this program.  If not, see <http://www.gnu.org/licenses/>.
// 
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using Manzana;
using System.IO;
using System.Collections;
using Tools;

namespace iPhoneGUI
{
    public partial class iPhoneList: Form
    {

        internal iPhone myPhone = new iPhone();
        internal Boolean connected = false;
        internal Boolean connecting = false;
        internal String lastSaveFolder = "";
        internal Boolean cancelCopy = false;
        internal Boolean showDotFolders = false;
        ItemProperties ipItems;

        internal enum TypeIdentifier
        {
            FileName,
            Extension,
            FullPath,
            FileType,
            HeaderBytes,
            HeaderString,
            ExtHeadBytes, // Extension First, then Header
            ExtHeadString
        }

        internal enum PreviewTypes { Image, Text, Music, Video, Document, Binary };

        internal class ItemProperty
        {
            public String Name;
            public iPhone.FileTypes Type;
            public TypeIdentifier Identifier;
            public String FileInfoText;
            public String ImageKey;
            public String Tag;
            public Byte[] Header;
            public Int32 ByteOffset;

            public ItemProperty(String inName) {
                Name = inName;
            }
            public override String ToString() {
                return Name;
            }
        }

        internal class ItemProperties
        {
            private ArrayList items;
            private Int32 selectedIndex;
            private Byte[] nullBytes = Hex.ToBytes("");
            private iPhone phone;
            public ItemProperties(iPhone iphone) {
                items = new ArrayList();
                phone = iphone;
            }
            public Boolean AddFileType( // for FileType Adds
                String name,
                iPhone.FileTypes type,
                String imageKey,
                String tag
                ) {
                return Add(name, type, TypeIdentifier.FileType, null, nullBytes, 0, imageKey, tag);
            }
            public Boolean AddFile( // for FileName, Extension, and FullPath
                String name,
                TypeIdentifier typeID,
                String fileInfoText,
                String imageKey,
                String tag
                ) {
                return Add(name, iPhone.FileTypes.File, typeID, fileInfoText, nullBytes, 0, imageKey, tag);
            }
            public Boolean AddFile( // for HeaderString and HeaderBytes
                String name,
                TypeIdentifier typeID,
                String headerString,
                Int32 byteOffset,
                String imageKey,
                String tag
                ) {
                Byte[] headerBytes;
                if ( typeID == TypeIdentifier.HeaderString ) {
                    headerBytes = TextString.ToByte(headerString);
                } else {
                    headerBytes = Hex.ToBytes(headerString);
                }
                return Add(name, iPhone.FileTypes.File, typeID, null, headerBytes, byteOffset, imageKey, tag);
            }
            public Boolean AddFile( // for ExtHeadString and ExtHeadBytes
                String name,
                TypeIdentifier typeID,
                String extension,
                String headerString,
                Int32 byteOffset,
                String imageKey,
                String tag
                ) {
                Byte[] headerBytes;
                if ( typeID == TypeIdentifier.ExtHeadString ) {
                    headerBytes = TextString.ToByte(headerString);
                } else {
                    headerBytes = Hex.ToBytes(headerString);
                }
                return Add(name, iPhone.FileTypes.File, typeID, extension, headerBytes, byteOffset, imageKey, tag);
            }
            public Boolean AddFolder( // for FileName and Extension
                String name,
                TypeIdentifier typeID,
                String folderInfoText,
                String imageKey,
                String tag
                ) {
                return Add(name, iPhone.FileTypes.Folder, typeID, folderInfoText, nullBytes, 0, imageKey, tag);
            }
            public Boolean AddPath( // for fixed pathname - can be file or folder
                String name,
                iPhone.FileTypes type,
                String folderInfoText,
                String imageKey,
                String tag
                ) {
                return Add(name, type, TypeIdentifier.FullPath, folderInfoText, nullBytes, 0, imageKey, tag);
            }
            public Boolean AddDevice( // for Devices
                String name,
                iPhone.FileTypes type,
                String imageKey,
                String tag
                ) {
                return Add(name, type, TypeIdentifier.FileType, null, nullBytes, 0, imageKey, tag);
            }

            public Boolean Add( // The Generic ADD
            String name,
            iPhone.FileTypes type,
            TypeIdentifier typeID,
            String fileInfoText,
            Byte[] headerBytes,
            Int32 byteOffset,
            String imageKey,
            String tag
            ) {
                ItemProperty newItem = new ItemProperty(name);
                newItem.Type = type;
                newItem.Identifier = typeID;
                newItem.FileInfoText = fileInfoText;
                newItem.Header = headerBytes;
                newItem.ByteOffset = byteOffset;
                newItem.ImageKey = imageKey;
                newItem.Tag = tag;
                items.Add(newItem);
                selectedIndex = items.Count - 1;
                return true;
            }
            public ItemProperty[] Items {
                get {
                    ItemProperty[] outItems = new ItemProperty[items.Count];
                    for ( Int32 i = 0; i < items.Count; i++ ) {
                        outItems[i] = (ItemProperty)items[i];
                    }
                    return outItems;
                }
            }
            public Int32 ItemIndex(String PropertyName) {
                for ( Int32 i = 0; i < items.Count; i++ ) {
                    if ( ((ItemProperty)items[i]).Name.Equals(PropertyName) ) {
                        selectedIndex = i;
                        return i;
                    }
                }
                return -1;
            }
            public ItemProperty Item(String PropertyName) {
                for ( Int32 i = 0; i < items.Count; i++ ) {
                    if ( ((ItemProperty)items[i]).Name.Equals(PropertyName) ) {
                        selectedIndex = i;
                        return (ItemProperty)items[i];
                    }
                }
                return null;
            }
            public ItemProperty FindItem(String fullPath) {
                /*Have to account for these: 
                 * FileName,    
                 * Extension,
                 * FullPath,    
                 * FileType,
                 * HeaderBytes, 
                 * HeaderString,
                 * ExtHeadBytes, // Extension First, then Header
                 * ExtHeadString
                 */
                /* I need to lay out the approach here...
                 * When we read a folder, we get back a list of all the folder entries,
                 * including the current / parent folder pointers.
                 * 
                 * I could break them up into FindFile / Folder / Device, but
                 * I may use the same criteria to locate them
                 * What's interesting with this particular approach is that the FIND
                 * function doesn't specify what to look for. We literally scan the rules
                 * and stop with the first MATCH.
                 * 
                 * Now since we store the FileType of each ItemProperty entry, we need to
                 * perform the first match on that attribute of the passed file.
                 * So FileType is the first piece of information we need.
                 */
                // Set variables to store whether we've already gathered a particular piece of info 
                String fileName = null;
                Boolean _fileName = false;
                String extension = null;
                Boolean _extension = false;
                
                ItemProperty returnItem = null;
                iPhone.FileTypes fileType = phone.FileType(fullPath);
                foreach ( ItemProperty item in items ) {
                    if ( fileType == item.Type ) {
                        switch (item.Identifier){
                            case TypeIdentifier.FileType:
                                returnItem = item;
                                break;
                            case TypeIdentifier.FullPath:
                                if (item.FileInfoText.Equals(fullPath.ToLower())){
                                    returnItem = item;
                                }
                                break;
                            case TypeIdentifier.FileName:
                                if (!_fileName){
                                    fileName = fullPath.Substring(fullPath.LastIndexOf("/")+1).ToLower();
                                    _fileName = true;
                                }
                                if (item.FileInfoText.Equals(fileName)){
                                    returnItem = item;
                                }
                                break;
                            case TypeIdentifier.Extension:
                                if (!_extension){
                                    extension = TextString.GetFileExtension(fullPath);
                                    _extension = true;
                                }
                                if (extension == item.FileInfoText){
                                    returnItem = item;
                                }
                                break;
                            case TypeIdentifier.ExtHeadString:
                            case TypeIdentifier.ExtHeadBytes:
                                if (!_extension){
                                    extension = TextString.GetFileExtension(fullPath);
                                    _extension = true;
                                }
                                if (extension == item.FileInfoText){
                                    Byte[] buffer = GetHeaderBytes(fullPath, item.ByteOffset, item.Header.Length);
                                    if (buffer == item.Header) {
                                        returnItem = item;
                                    }
                                }
                                break;
                            case TypeIdentifier.HeaderBytes:
                            case TypeIdentifier.HeaderString:
                                Byte[] fileBuffer = GetHeaderBytes(fullPath, item.ByteOffset, item.Header.Length);
                                if (fileBuffer == item.Header) {
                                    returnItem = item;
                                }
                                break;
                        }
                    }
                    if (returnItem != null) {
                        break;
                    }
                }
                return returnItem;
            }
            public ItemProperty SelectedItem {
                get { return (ItemProperty)items[selectedIndex]; }
            }
            public Int32 SelectedIndex {
                get { return selectedIndex; }
                set {
                    if ( value >= 0 && value < items.Count )
                        selectedIndex = value;
                }
            }
            private Byte[] GetHeaderBytes(String fileName, Int32 offset, Int32 length){
                Byte[] retval = null;
                using (Stream fileStream = iPhoneFile.OpenRead(phone, fileName)){
                    Byte[] buffer = new Byte[length];
                    Int32 bytesRead;
                    if ((offset + length) <= phone.FileSize(fileName)) {
                        bytesRead = fileStream.Read(buffer, offset, length);
                        retval = buffer;
                    }
                }
                return retval;
            }
        }

        public iPhoneList() {
            InitializeComponent();
            SetObjectSizes();
            SetStatus();
            //myPhone.Connect += new ConnectEventHandler(Connecting);
            //myPhone.Disconnect += new ConnectEventHandler(Connecting);
            // TEMPORARY FileType load until I add a FileType config window
            // Files
            ipItems = new ItemProperties(myPhone);
            ipItems.AddFile("Program", TypeIdentifier.HeaderBytes, "CEFAEDFE0C00", 0, "Program", "Program");
            ipItems.AddFile("Application", TypeIdentifier.Extension, ".app", "App", "Application");
            ipItems.AddFile("BinPList", TypeIdentifier.ExtHeadBytes, ".plist", "62706C6973743030", 0, "Settings", "Settings");
            ipItems.AddFile("PList", TypeIdentifier.Extension, ".plist", "Settings", "Settings");
            ipItems.AddFile("BinStrings", TypeIdentifier.ExtHeadBytes, ".strings", "62706C6973743030", 0, "Settings", "Settings");
            ipItems.AddFile("Strings", TypeIdentifier.Extension, ".strings", "Settings", "Settings");
            ipItems.AddFile("Thumnail", TypeIdentifier.Extension, ".thm", "Image", "Image");
            ipItems.AddFile("Thumb", TypeIdentifier.Extension, ".ithmb", "Image", "Image");
            ipItems.AddFile("PNG", TypeIdentifier.Extension, ".png", "Image", "Image");
            ipItems.AddFile("JPG", TypeIdentifier.Extension, ".jpg", "Image", "Image");
            ipItems.AddFile("GIF", TypeIdentifier.Extension, ".gif", "Image", "Image");
            ipItems.AddFile("BMP", TypeIdentifier.Extension, ".bmp", "Image", "Image");
            ipItems.AddFile("AAC", TypeIdentifier.Extension, ".aac", "Audio", "Audio");
            ipItems.AddFile("MP3", TypeIdentifier.Extension, ".mp3", "Audio", "Audio");
            ipItems.AddFile("M4A", TypeIdentifier.Extension, ".m4a", "Audio", "Audio");
            ipItems.AddFile("Photo DataBase", TypeIdentifier.FileName, "", "Database", "Database");
            ipItems.AddFile("ArtworkDB", TypeIdentifier.FileName, "", "Database", "Database");
            ipItems.AddFile("Text", TypeIdentifier.Extension, ".txt", "Document", "Document");
            ipItems.AddFile("Script", TypeIdentifier.Extension, ".script", "Script", "Script");
            ipItems.AddFile("ShellScript", TypeIdentifier.Extension, ".sh", "Script", "Script");
            ipItems.AddFile("File", TypeIdentifier.FileType, "", "Other", "Unknown");
            // Folder types
            ipItems.AddFolder("App Folder", TypeIdentifier.Extension, ".app", "Folder-App", "App Folder");
            ipItems.AddFolder("Photos", TypeIdentifier.FileName, "photos", "Folder-Image", "Image Folder");
            ipItems.AddFolder("DCIM", TypeIdentifier.FileName, "dcim", "Folder-Image", "Image Folder");
            ipItems.AddFolder("100APPLE", TypeIdentifier.FileName, "100apple", "Folder-Image", "Image Folder");
            ipItems.AddFolder("Artwork", TypeIdentifier.FileName, "artwork", "Folder-Image", "Image Folder");
            ipItems.AddFolder("Thumbs", TypeIdentifier.FileName, "thumbs", "Folder-Image", "Image Folder");
            ipItems.AddFolder("Music", TypeIdentifier.FileName, "music", "Folder-Audio", "Audio Folder");
            ipItems.AddFolder("Folder", TypeIdentifier.FileType, "", "Folder", "Folder");
            // Other types
            ipItems.AddDevice("CharDevice", iPhone.FileTypes.CharDevice, "Device", "Character Device");
            ipItems.AddDevice("BlockDevice", iPhone.FileTypes.BlockDevice, "Device", "Block Device");
            ipItems.AddDevice("FIFO", iPhone.FileTypes.FIFO, "Device", "FIFO");
        }

        private void SetObjectSizes() {
            labelStatus.Width = statusMain.Width - 120;
        }

        private void SetStatus() {
            if ( myPhone.IsConnected ) {
                labelStatus.Text = "iPhone is connected.";
            } else {
                labelStatus.Text = "iPhone is not connected.";
            }
        }

        private void timerMain_Tick(object sender, EventArgs e) {
            if ( !connecting && !connected ) {
                if ( myPhone.IsConnected ) {
                    connecting = true;
                    //Connecting(myPhone, null);
                    this.treeFolders.Nodes.Clear();
                    FillTree();
                    ShowFiles(treeFolders.TopNode, "/");
                    this.Show();
                    connecting = false;
                    connected = true;
                    SetStatus();
                } else {
                    if ( connected ) {
                        MessageBox.Show("iPhone Disconnected.");
                        this.treeFolders.Nodes.Clear();
                        this.listFiles.Clear();
                        SetStatus();
                    }
                }
            }
        }

        private void RefreshView() {
            FillTree();
            ShowFiles();
            SetStatus();
        }

        public void FillTree() {
            treeFolders.Nodes.Clear();
            treeFolders.Nodes.Add("/", "/");
            FillTree(treeFolders.TopNode, treeFolders.TopNode.Text);
        }

        public void FillTree(TreeNode thisNode, String path) {
            thisNode.Nodes.Clear();
            AddNodes(thisNode, path);

            //treeFolders.Nodes.Add(AddNodes(path, name, levels));
            treeFolders.Nodes[0].Expand();
            treeFolders.SelectedNode = thisNode;
            SetStatus();
        }

        public void EmptyTree() {
            treeFolders.Nodes.Clear();
            listFiles.Items.Clear();
        }

        public void AddNodes(TreeNode thisNode, String path) {
            AddNodes(thisNode, path, 1);
        }

        public void AddNodes(TreeNode thisNode, String path, Int32 getLevels) {
            String[] dirNames;
            labelStatus.Text = "Adding folder " + thisNode.Text;
            dirNames = myPhone.GetDirectories(path);
            if ( dirNames.Length > 0 ) {
                if ( getLevels != 0 ) {
                    for ( int i = 0; i < dirNames.Length; i++ ) {
                        TreeNode childNode = new TreeNode(dirNames[i]);
                        childNode.ImageKey = "Folder";
                        childNode.Name = path + "/" + dirNames[i];
                        ItemProperty thisItem = ipItems.FindItem(childNode.Name);
                        if (thisItem != null){
                            childNode.ImageKey = thisItem.ImageKey;
                            childNode.Tag = thisItem.Tag;
                        } else {
                            childNode.ImageKey = "Other";
                            childNode.Tag = "Unknown";
                        }
                        AddNodes(childNode, path + "/" + dirNames[i], (getLevels - 1));
                        thisNode.Nodes.Add(childNode);
                        thisNode.Tag = "loaded";
                        Application.DoEvents();
                    }
                } else {
                    TreeNode childNode = new TreeNode(".");
                    childNode.Name = path + "/.";
                    thisNode.Nodes.Add(childNode);
                    if ( thisNode.ImageKey == "Folder" )
                        thisNode.ImageKey = "Folder-Files";
                    thisNode.Tag = "notloaded";
                    Application.DoEvents();
                }
            }

        }

        private void btnRefresh_Click(object sender, EventArgs e) {
            RefreshFiles();
        }

        private void RefreshFiles() {
            myPhone.GetFiles("/");
            FillTree();
            this.Show();
            connecting = false;
            connected = true;
        }

        private void treeFolders_AfterSelect(object sender, TreeViewEventArgs e) {
            String currentPath = e.Node.FullPath.Replace("\\", "/");
            ShowFiles(e.Node, currentPath); // showFiles should get the dirlist, too (unless it's already gotten)
            SetStatus();
        }

        private void ShowFiles() {
            ShowFiles(treeFolders.TopNode, "/");
        }

        private void ShowFiles(TreeNode thisNode, String path) {
            Boolean addNodes = false;
            if ( thisNode.Tag == null || thisNode.Tag.ToString() == "notloaded" ) {
                addNodes = true;
                thisNode.Nodes.Clear();
            }
            String[] files = myPhone.GetFiles(path);
            this.listFiles.Items.Clear();
            Int32 fileSize;
            iPhone.FileTypes fileType;
            foreach ( String file in files ) {
                if ( !(file.Equals(".") || file.Equals("..")) || showDotFolders ) {
                    String fullPath = path + "/" + file;
                    ListViewItem thisFile = new ListViewItem(file);
                    thisFile.ImageKey = "Other";
                    myPhone.GetFileInfoDetails(fullPath, out fileSize, out fileType);
                    thisFile.SubItems.Add(fileSize.ToString());
                    ItemProperty thisItem;
                    if ( (thisItem = ipItems.FindItem(fullPath)) != null ) {
                        thisFile.ImageKey = thisItem.ImageKey;
                        thisFile.Tag = thisItem.Tag;
                        thisFile.SubItems.Add(thisItem.Name);
                    } else {
                        thisFile.ImageKey = "Other";
                        thisFile.Tag = "Unknown";
                        thisFile.SubItems.Add("File");
                    }
                    if ( (fileType == iPhone.FileTypes.Folder) && addNodes ) {
                        TreeNode childNode = new TreeNode(file);
                        childNode.ImageKey = "Folder";
                        childNode.Name = fullPath;
                        AddNodes(childNode, fullPath, 0);
                        thisNode.Nodes.Add(childNode);
                        thisNode.Tag = "loaded";
                        Application.DoEvents();
                    }
                    thisFile.Group = listFiles.Groups[listFiles.Groups.Count - 1]; // Other Group
                    for ( Int32 i = 0; i < listFiles.Groups.Count; i++ ) {
                        if ( thisFile.Tag == listFiles.Groups[i].Tag ) {
                            thisFile.Group = listFiles.Groups[i];
                            break;
                        }
                    }
                    listFiles.Items.Add(thisFile);
                }
            }
        }

        private String GetFileExt(String fileName) {
            Int32 period = fileName.LastIndexOf(".");
            String retVal;
            if ( period > 0 ) {
                retVal = fileName.Substring(period);
            } else {
                retVal = "";
            }
            return retVal;
        }

        private void listFiles_DragEnter(object sender, DragEventArgs e) {
            if ( ((e.AllowedEffect & DragDropEffects.Copy) != 0) && (e.Data.GetDataPresent("FilenameW")) ) {
                e.Effect = DragDropEffects.Copy;
            }
        }

        private void listFiles_DragDrop(object sender, DragEventArgs e) {
            TreeNode thisNode = treeFolders.SelectedNode;
            String destPath = thisNode.FullPath.Replace("\\", "/");
            String[] files = (String[])e.Data.GetData(DataFormats.FileDrop);

            for ( int i = 0; i < files.Length; i++ ) {
                CopyToDevice(files[i], destPath);
            }
            // Force a refresh
            FillTree(thisNode, destPath);
            ShowFiles(thisNode, destPath);
        }

        internal void CopyToDevice(String srcFile, String destPath) {
            FileInfo thisFile = new FileInfo(srcFile);
            Console.WriteLine(thisFile.Attributes.ToString());
            if ( thisFile.Attributes == FileAttributes.Directory ) {
                String[] files = Directory.GetFiles(thisFile.FullName);
                for ( Int32 i = 0; i < files.Length; i++ ) {
                    String newDirectory = destPath + "/" + thisFile.Name;
                    myPhone.CreateDirectory(newDirectory);
                    CopyToDevice(files[i], newDirectory);
                }
            } else {
                Byte[] fileBuffer = new Byte[1024];
                Int32 length;
                labelStatus.Text = "Copying " + thisFile.FullName;
                using ( Stream inStream = File.OpenRead(thisFile.FullName) ) {
                    using ( Stream outStream =
                        iPhoneFile.OpenWrite(myPhone, destPath + "/" + Path.GetFileName(thisFile.FullName)) ) {
                        while ( (length = inStream.Read(fileBuffer, 0, fileBuffer.Length)) > 0 ) {
                            outStream.Write(fileBuffer, 0, length);
                        }
                    }
                }
                Application.DoEvents();
            }
        }


        private void treeFolders_BeforeExpand(object sender, TreeViewCancelEventArgs e) {
            if ( e.Node.Tag.ToString() == "notloaded" ) {
                FillTree(e.Node, e.Node.FullPath.Replace("\\", "/"));
            }
        }

        private void folderToolStripMenuItem_Click(object sender, EventArgs e) {
            Console.WriteLine(e.ToString());

        }

        private void DeleteSelectedItems() {
            String path = treeFolders.SelectedNode.FullPath.Replace("\\", "/");
            TreeNode thisNode = treeFolders.SelectedNode;
            Boolean deletedFolder = false;
            if ( listFiles.SelectedItems.Count > 0 ) {
                foreach ( ListViewItem item in listFiles.SelectedItems ) {
                    if ( !item.Name.Equals(".") && !item.Name.Equals("..") ) {
                        Console.WriteLine(path + ", " + item.Name);
                        if ( myPhone.IsDirectory(path + "/" + item.Text) )
                            deletedFolder = true;
                        myPhone.DeleteFromDevice(path + "/" + item.Text);
                    }
                }
            }
            if ( deletedFolder ) {
                FillTree(thisNode, path);
            }
            ShowFiles(thisNode, path);
        }

        private void CreateFolder() {
            timerMain.Enabled = false;
            TreeNode selectedNode = treeFolders.SelectedNode;
            String inPath = selectedNode.FullPath.Replace("\\", "/");
            using ( NewFolderForm frmNew = new NewFolderForm() ) {
                frmNew.ActionText = "Folder Name: " + inPath + "/";
                frmNew.ShowDialog();
                if ( frmNew.DialogResult == DialogResult.OK ) {
                    try {
                        myPhone.CreateDirectory(inPath + "/" + frmNew.FolderName);
                        FillTree(selectedNode, inPath);
                        ShowFiles(selectedNode, inPath);
                    }
                    catch ( Exception err ) {
                        MessageBox.Show(err.Message);
                    }
                }
            }
            timerMain.Enabled = true;
        }

        private void CopyItemsFromDevice() {
            toolItemCancel.Visible = true;
            Boolean okToCopy = true;
            Boolean copyAll = false;
            String fromPath = "/";
            if ( listFiles.SelectedItems.Count == 0 ) {
                if ( treeFolders.SelectedNode == null ) {
                    MessageBox.Show("Nothing Selected to Copy", "iPhoneList Message:");
                    okToCopy = false;
                } else {
                    copyAll = true;
                }
            } else {
                if ( treeFolders.SelectedNode != null ) {
                    fromPath = treeFolders.SelectedNode.FullPath.Replace("\\", "/");
                }
            }
            if ( okToCopy ) {
                using ( FolderBrowserDialog saveTo = new FolderBrowserDialog() ) {
                    saveTo.SelectedPath = lastSaveFolder;
                    DialogResult result = saveTo.ShowDialog();
                    if ( result == DialogResult.OK ) {
                        String savePath = saveTo.SelectedPath;
                        lastSaveFolder = savePath;
                        if ( copyAll ) {
                            foreach ( ListViewItem item in listFiles.Items ) {
                                CopyItemFromDevice(savePath, fromPath, item.Text);
                            }
                        } else {
                            foreach ( ListViewItem item in listFiles.SelectedItems ) {
                                CopyItemFromDevice(savePath, fromPath, item.Text);
                            }
                        }
                    }
                }
            }
            toolItemCancel.Visible = false;
        }

        private Boolean CopyItemFromDevice(String savePath, String fromPath, String item) {
            Boolean continueCopy = true;
            String itemPath = fromPath + "/" + item;
            if ( !item.Equals(".") && !item.Equals("..") ) {
                if ( myPhone.IsDirectory(itemPath) ) {
                    String newPath = savePath + "\\" + item;
                    Directory.CreateDirectory(newPath);
                    String[] items = myPhone.GetFiles(itemPath);
                    for ( Int32 i = 0; i < items.Length; i++ ) {
                        continueCopy = CopyItemFromDevice(newPath, itemPath, items[i]);
                        if ( !continueCopy ) break;
                    }
                } else {
                    String sourcePath = fromPath + "/" + item;
                    String destPath = savePath + "\\" + item;
                    iPhone.FileTypes fileType = myPhone.FileType(sourcePath);
                    if ( fileType == iPhone.FileTypes.Folder ||
                        fileType == iPhone.FileTypes.File ) {
                        labelStatus.Text = "Copying: " + sourcePath;
                        if ( item.Contains(".plist") ) {
                            DecodePListStream(sourcePath, destPath);
                        }
                        Byte[] fileBuffer = new Byte[1024];
                        Int32 length;
                        Int32 bytesSoFar = 0;
                        toolStripProgressBar1.Minimum = 0;
                        using ( Stream inStream = iPhoneFile.OpenRead(myPhone, sourcePath) ) {
                            toolStripProgressBar1.Maximum = (Int32)inStream.Length;
                            using ( Stream outStream = File.OpenWrite(destPath) ) {
                                try {
                                    while ( (length = inStream.Read(fileBuffer, 0, fileBuffer.Length)) > 0 && !cancelCopy ) {
                                        bytesSoFar += length;
                                        toolStripProgressBar1.Value = bytesSoFar;
                                        Application.DoEvents();
                                        if ( cancelCopy ) {
                                            if ( MessageBox.Show("Cancel Copying?", "iPhoneList Message", MessageBoxButtons.YesNo) == DialogResult.No ) {
                                                cancelCopy = false;
                                            }
                                        }
                                        if ( !cancelCopy ) {
                                            outStream.Write(fileBuffer, 0, length);
                                        }
                                    }
                                }
                                catch ( IOException err ) {
                                    DialogResult retVal = MessageBox.Show("iPhone stopped responding on file: " + sourcePath + ".\n Attempt to Continue?", err.Message, MessageBoxButtons.YesNo);
                                    if ( retVal == DialogResult.No ) {
                                        continueCopy = false;
                                    }
                                }
                            }
                        }
                    } else {
                        labelStatus.Text = "Skipping non-File: " + sourcePath;
                    }
                    Application.DoEvents();
                }
            }
            return continueCopy;
        }

        private void DecodePListStream(String inFile, String outFile) {
            Byte[] fileBuffer = new Byte[1024];
            Int32 length;
            using ( Stream inStream = iPhoneFile.OpenRead(myPhone, inFile) ) {
                using ( Stream outStream = File.OpenWrite(outFile) ) {
                    while ( (length = inStream.Read(fileBuffer, 0, fileBuffer.Length)) > 0 ) {
                        outStream.Write(fileBuffer, 0, length);
                    }
                }
            }
        }

        private String DecodePListFile(String inFile) {
            return ReadFile(inFile, 1024);
        }

        private String ReadFile(String inFile, Int32 length) {
            StringBuilder text = new StringBuilder();
            Byte[] fileBuffer = new Byte[1024];
            Int32 maxBytes, bytesRead = 0;
            Int32 bufferSize;
            if ( length == -1 ) {
                maxBytes = myPhone.FileSize(inFile);
            } else {
                maxBytes = length;
            }
            using ( Stream inStream = iPhoneFile.OpenRead(myPhone, inFile) ) {
                while ( (bufferSize = inStream.Read(fileBuffer, 0, fileBuffer.Length)) > 0 &&
                    bytesRead <= maxBytes ) {
                    bytesRead += bufferSize;
                    text.Append(System.Text.Encoding.ASCII.GetString(fileBuffer));
                }
            }
            return String.Join(Environment.NewLine, text.ToString().Split('\n'));
        }

        private void PreviewSelectedItem(TreeNode thisNode, ListViewItem item) {
            if ( thisNode == null ) {
                thisNode = treeFolders.TopNode;
            }
            String fullName = thisNode.FullPath.Replace("\\", "/") + "/" + item.Text;

            iPhone.FileTypes fileType = myPhone.FileType(fullName);
            String previewText = null;
            Image previewImage = null;
            PreviewTypes previewType = PreviewTypes.Binary;
            switch ( fileType ) {
                case iPhone.FileTypes.File:
                    switch ( GetFileExt(item.Text) ) {
                        case ".plist":
                            previewText = DecodePListFile(fullName);
                            previewType = PreviewTypes.Text;
                            break;
                        case ".mp3":
                            previewImage = imageFilesLarge.Images["MP3.ico"];
                            previewType = PreviewTypes.Music;
                            break;
                        case ".m4a":
                            previewImage = imageFilesLarge.Images["M3U.ico"];
                            previewType = PreviewTypes.Music;
                            break;
                        case ".aac":
                            previewImage = imageFilesLarge.Images["ASX.ico"];
                            previewType = PreviewTypes.Music;
                            break;
                        default:
                            previewText = ReadFile(fullName, 1024);
                            previewType = PreviewTypes.Text;
                            break;
                    }
                    break;
                default:
                    previewImage = imageFilesLarge.Images["document.ico"];
                    previewType = PreviewTypes.Binary;
                    break;
            }
            switch ( previewType ) {
                case PreviewTypes.Text:
                    previewTextBox.Text = previewText;
                    previewTextBox.Visible = true;
                    previewImageBox.Visible = false;
                    break;
                default:
                    previewTextBox.Visible = false;
                    previewImageBox.Visible = true;
                    previewImageBox.Image = previewImage;
                    break;
            }
        }

        private void popupFilesGetFiles_Click(object sender, EventArgs e) {
            CopyItemsFromDevice();
        }

        private void popupFilesDelete_Click(object sender, EventArgs e) {
            DeleteSelectedItems();
        }

        private void toolItemRefresh_Click(object sender, EventArgs e) {
            RefreshView();
        }

        private void popupTreeCreateFolder_Click(object sender, EventArgs e) {
            CreateFolder();
        }

        private void popupTreeRefresh_Click(object sender, EventArgs e) {
            RefreshView();
        }

        private void iPhoneList_Resize(object sender, EventArgs e) {
            SetObjectSizes();
        }

        private void toolItemCancel_Click(object sender, EventArgs e) {
            cancelCopy = true;
        }

        private void listFiles_SelectedIndexChanged(object sender, EventArgs e) {
            if ( listFiles.SelectedItems.Count == 1 ) {
                PreviewSelectedItem(treeFolders.SelectedNode, listFiles.SelectedItems[0]);
            } else {
                previewTextBox.Text = "";
                previewTextBox.Visible = true;
                previewImageBox.Visible = false;
            }
        }

        private void toolItemViewSmallIcons_Click(object sender, EventArgs e) {
            if ( sender.Equals(toolItemViewSmallIcons) )
                listFiles.View = View.SmallIcon;
            else if ( sender.Equals(toolItemViewLargeIcons) )
                listFiles.View = View.LargeIcon;
            else if ( sender.Equals(toolItemViewDetails) )
                listFiles.View = View.Details;
            else
                listFiles.View = View.List;
        }

        private void listFiles_DoubleClick(object sender, EventArgs e) {
            if ( listFiles.SelectedItems.Count == 1 ) {
                String thisFile = listFiles.SelectedItems[0].Text;
                String path = treeFolders.SelectedNode.FullPath.Replace("\\", "/");
                String fullPath = path + "/" + thisFile;

                iPhone.FileTypes fileType;
                if ( (fileType = myPhone.FileType(fullPath)) == iPhone.FileTypes.Folder ) {
                    TreeNode[] nodes = treeFolders.SelectedNode.Nodes.Find(fullPath, true);
                    treeFolders.SelectedNode = nodes[0];
                }
            }
        }
    }
}