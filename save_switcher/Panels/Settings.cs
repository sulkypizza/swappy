using Microsoft.Win32;
using save_switcher.Elements;
using SharpDX;
using SharpDX.Direct2D1;
using SharpDX.Mathematics.Interop;
using System;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace save_switcher.Panels
{
    internal class Settings : Panel, IDisposable
    {
        private DeviceContext deviceContext;

        private LinearGradientBrush backgroundBrush;

        private SimpleButton addGameButton;
        private SimpleButton addSyncButton;
        private SimpleButton listGamesButton;

        private DatabaseManager dbManager;

        public override void Initialize(DeviceContext deviceContext, params object[] args)
        {
            Application.EnableVisualStyles(); //TODO: remove this after replacing the windows forms with a custom UI
            this.deviceContext = deviceContext;

            dbManager = new DatabaseManager();

            createSizeDependantResources();

            Program.GetProgramForm().KeyDown += OnKeyDown;
        }

        public void Dispose()
        {
            Program.GetProgramForm().KeyDown -= OnKeyDown;

            addGameButton.Dispose();
            addSyncButton.Dispose();
        }

        public void OnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape)
                Program.ChangePanel<ProfileSelector>();
        }

        private void createSizeDependantResources()
        {
            LinearGradientBrushProperties backGroundGradientProperties = new LinearGradientBrushProperties
            {
                StartPoint = new RawVector2(0, 0),
                EndPoint = new RawVector2(deviceContext.Size.Width, deviceContext.Size.Height),
            };
            GradientStop[] backgroundGradientStops = new GradientStop[]
            {
                new GradientStop()
                {
                    //purple
                    Color = new Color(137, 41, 73),
                    Position = 0,
                },

                new GradientStop()
                {
                    //regular blue
                    Color = new Color(67, 106, 172),
                    Position = 0.7f,
                },

                new GradientStop()
                {
                    //light blue
                    Color = new Color(67, 183, 184),
                    Position = 0.9f,
                }
            };
            backgroundBrush = new LinearGradientBrush(deviceContext, backGroundGradientProperties, new GradientStopCollection(deviceContext, backgroundGradientStops, ExtendMode.Wrap));

            var baseButtonSize = new Size2(500, 100);
            var addGameProperties = new SimpleButtonProperties()
            {
                Size = baseButtonSize,
                Position = new Vector2(deviceContext.Size.Width / 2 - baseButtonSize.Width / 2, 200),
                
                Text = "Add Game",
                TextColor = Color.White,
                FontSize = 30f,

                BorderThickness = 5f,
                CornerRadius = 10f,

                ButtonColor = new Color(61, 82, 191),
                ButtonHoverColor = new Color(74, 93, 186),
                ButtonPressedColor = new Color(54, 74, 175),

                BorderColor = new Color(76, 98, 211),
                BorderHoverColor = new Color(95, 114, 211),
                BorderPressedColor = new Color(63, 85, 193),
            };

            var addSyncProperties = new SimpleButtonProperties()
            {
                Size = baseButtonSize,
                Position = new Vector2(deviceContext.Size.Width / 2 - baseButtonSize.Width / 2, 400),

                Text = "Add Sync",
                TextColor = Color.White,
                FontSize = 30f,

                BorderThickness = 5f,
                CornerRadius = 10f,

                ButtonColor = new Color(61, 82, 191),
                ButtonHoverColor = new Color(74, 93, 186),
                ButtonPressedColor = new Color(54, 74, 175),

                BorderColor = new Color(76, 98, 211),
                BorderHoverColor = new Color(95, 114, 211),
                BorderPressedColor = new Color(63, 85, 193),
            };

            var listGamesProperties = new SimpleButtonProperties()
            {
                Size = baseButtonSize,
                Position = new Vector2(deviceContext.Size.Width / 2 - baseButtonSize.Width / 2, 600),

                Text = "View Games",
                TextColor = Color.White,
                FontSize = 30f,

                BorderThickness = 5f,
                CornerRadius = 10f,

                ButtonColor = new Color(61, 82, 191),
                ButtonHoverColor = new Color(74, 93, 186),
                ButtonPressedColor = new Color(54, 74, 175),

                BorderColor = new Color(76, 98, 211),
                BorderHoverColor = new Color(95, 114, 211),
                BorderPressedColor = new Color(63, 85, 193),
            };

            addGameButton = new SimpleButton(deviceContext, addGameProperties, addGameButtonAction);
            addSyncButton = new SimpleButton(deviceContext, addSyncProperties, addSyncButtonAction);
            listGamesButton = new SimpleButton(deviceContext, listGamesProperties, listGamesButtonAction);
            

            InputNavigable.ConnectNeighbors(addGameButton, addSyncButton, NavigateDirection.Down);
            InputNavigable.ConnectNeighbors(addSyncButton, listGamesButton, NavigateDirection.Down);

            addGameButton.Select();
        }

        public override void Resize(DeviceContext deviceContext)
        {
            this.deviceContext = deviceContext;
            createSizeDependantResources();
        }

        public override void Update()
        {
            addGameButton.Update();
            addSyncButton.Update();
            listGamesButton.Update();
        }

        public override void Draw(DeviceContext deviceContext)
        {
            deviceContext.BeginDraw();
            deviceContext.Clear(Color.Black);
            deviceContext.FillRectangle(new RawRectangleF(0, 0, deviceContext.Size.Width, deviceContext.Size.Height), backgroundBrush);

            addGameButton.Draw();
            addSyncButton.Draw();
            listGamesButton.Draw();

            deviceContext.EndDraw();
        }

        private void listGamesButtonAction()
        {

            Form f = new Form();
            f.Text = "Game Viewer";
            f.Size = new System.Drawing.Size(800, 600);
            f.Icon = new System.Drawing.Icon("Media/swappy_icon.ico");
            f.StartPosition = FormStartPosition.CenterParent;
            f.FormBorderStyle = FormBorderStyle.FixedSingle;

            TreeView tv = new TreeView();
            tv.Size = new System.Drawing.Size(f.Size.Width - 50, f.Size.Height - 150);
            tv.Location = new System.Drawing.Point(15, 15);

            void populateTree()
            {
                Game[] games = dbManager.GetAllGames();
                tv.Nodes.Clear();

                foreach (Game game in games ?? Enumerable.Empty<Game>())
                {
                    TreeNode node = new TreeNode(game.Name);
                    node.Tag = game;

                    foreach (SyncDefinition def in dbManager.GetSyncDefinitions(game.ID) ?? Enumerable.Empty<SyncDefinition>())
                    {
                        TreeNode sNode = new TreeNode(def.SyncSource);
                        sNode.Tag = def;

                        node.Nodes.Add(sNode);
                    }

                    tv.Nodes.Add(node);
                }
            }

            populateTree();

            System.Windows.Forms.Button detailsButton = new System.Windows.Forms.Button();
            detailsButton.Text = "Details...";
            detailsButton.Size = new System.Drawing.Size(200, 30);
            detailsButton.Location = new System.Drawing.Point(f.Size.Width - 250, f.Size.Height - 100);
            detailsButton.Click += (_, __) =>
            {
                Form df = new Form();
                df.Icon = new System.Drawing.Icon("Media/swappy_icon.ico");
                df.Text = "Details";
                df.BackColor = System.Drawing.Color.White;
                df.StartPosition = FormStartPosition.CenterParent;
                df.FormBorderStyle = FormBorderStyle.FixedSingle;
                df.AutoSize = true;
                df.AutoSizeMode = AutoSizeMode.GrowAndShrink;

                TreeNode n = tv.SelectedNode;

                if (n?.Tag is Game)
                {
                    Game g = (Game)n.Tag;

                    df.Controls.Add(new Label() { Text = "Name:", Location = new System.Drawing.Point(20, 20), AutoSize = true });
                    df.Controls.Add(new Label() { Text = "ID:", Location = new System.Drawing.Point(20, 45), AutoSize = true });
                    df.Controls.Add(new Label() { Text = "Executable:", Location = new System.Drawing.Point(20, 70), AutoSize = true });
                    df.Controls.Add(new Label() { Text = "Arguments:", Location = new System.Drawing.Point(20, 95), AutoSize = true });

                    df.Controls.Add(new Label() { Text = $"{g.Name}", Location = new System.Drawing.Point(100, 20), AutoSize = true });
                    df.Controls.Add(new Label() { Text = $"{g.ID}", Location = new System.Drawing.Point(100, 45), AutoSize = true });
                    df.Controls.Add(new Label() { Text = $"{g.Exec}", Location = new System.Drawing.Point(100, 70), AutoSize = true });
                    df.Controls.Add(new Label() { Text = $"{g.Args}", Location = new System.Drawing.Point(100, 95), AutoSize = true });

                    var okButton = new System.Windows.Forms.Button() { Text = "OK", Location = new System.Drawing.Point(df.Size.Width - 120, 150), Size = new System.Drawing.Size(100, 30), Margin=new Padding(10) };
                    okButton.Click += (a, b) => { df.Close(); };

                    df.Controls.Add(okButton);

                    df.ShowDialog();
                }
                else if (n?.Tag is SyncDefinition)
                {
                    SyncDefinition s = (SyncDefinition)n.Tag;

                    df.Controls.Add(new Label() { Text = "ID:", Location = new System.Drawing.Point(20, 20), AutoSize = true });
                    df.Controls.Add(new Label() { Text = "Game ID:", Location = new System.Drawing.Point(20, 45), AutoSize = true });
                    df.Controls.Add(new Label() { Text = "Type:", Location = new System.Drawing.Point(20, 70), AutoSize = true });
                    df.Controls.Add(new Label() { Text = "Sync Source:", Location = new System.Drawing.Point(20, 95), AutoSize = true });
                    df.Controls.Add(new Label() { Text = "Description:", Location = new System.Drawing.Point(20, 120), AutoSize = true });

                    df.Controls.Add(new Label() { Text = $"{s.SyncDefinitionId}", Location = new System.Drawing.Point(110, 20), AutoSize = true });
                    df.Controls.Add(new Label() { Text = $"{s.GameID}", Location = new System.Drawing.Point(110, 45), AutoSize = true });
                    df.Controls.Add(new Label() { Text = $"{s.Type}", Location = new System.Drawing.Point(110, 70), AutoSize = true });
                    df.Controls.Add(new Label() { Text = $"{s.SyncSource}", Location = new System.Drawing.Point(110, 95), AutoSize = true });
                    df.Controls.Add(new Label() { Text = $"{s.Description}", Location = new System.Drawing.Point(110, 120), AutoSize = true });

                    var okButton = new System.Windows.Forms.Button() { Text = "OK", Location = new System.Drawing.Point(df.Size.Width - 120, 175), Size = new System.Drawing.Size(100, 30), Margin = new Padding(10) };
                    okButton.Click += (a, b) => { df.Close(); };

                    df.Controls.Add(okButton);

                    df.ShowDialog();
                }
            };

            System.Windows.Forms.Button deleteButton = new System.Windows.Forms.Button();
            deleteButton.Text = "Delete Item";
            deleteButton.Size = new System.Drawing.Size(200, 30);
            deleteButton.Location = new System.Drawing.Point(50, f.Size.Height - 100);

            deleteButton.Click += (_, __) => 
            {
                if(tv.SelectedNode.Tag is Game g)
                {
                    if(MessageBox.Show($"Are you sure you want to delete the game {g.Name}?\n This is not recoverable and could delete save data!", "Are you sure?", 
                        MessageBoxButtons.OKCancel, MessageBoxIcon.Warning) == DialogResult.OK)
                    {
                        if (dbManager.DeleteGame(g.ID))
                            MessageBox.Show("Game successfully deleted.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        else
                            MessageBox.Show("An error occurred.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
                else if(tv.SelectedNode.Tag is SyncDefinition sd)
                {
                    if (MessageBox.Show($"Are you sure you want to delete the sync definition {sd.SyncSource}?\n This is not recoverable and could delete save data!", "Are you sure?",
                        MessageBoxButtons.OKCancel, MessageBoxIcon.Warning) == DialogResult.OK)
                    {
                        if (dbManager.DeleteSyncDef(sd.SyncDefinitionId))
                            MessageBox.Show("Sync definition successfully deleted.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        else
                            MessageBox.Show("An error occurred.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }

                populateTree();
            };

            f.Controls.Add(tv);
            f.Controls.Add(detailsButton);
            f.Controls.Add(deleteButton);

            f.ShowDialog();

        }

        private void addSyncButtonAction()
        {
            Form f = new Form();
            f.Icon = new System.Drawing.Icon("Media/swappy_icon.ico");
            f.Text = "Add Sync";
            f.Size = new System.Drawing.Size(670, 200);
            f.StartPosition = FormStartPosition.CenterParent;
            f.FormBorderStyle = FormBorderStyle.FixedSingle;
            f.AutoSize = true;
            f.AutoSizeMode = AutoSizeMode.GrowAndShrink;

            Game[] games = dbManager.GetAllGames();

            f.Controls.Add(new Label() { Text = "Game:", Location = new System.Drawing.Point(10, 13), AutoSize = true });

            ComboBox cb = new ComboBox() { Location = new System.Drawing.Point(70, 10), Size = new System.Drawing.Size(400, 15) };

            foreach (Game g in games ?? Enumerable.Empty<Game>())
            {
                cb.Items.Add(g.Name);
            }

            f.Controls.Add(new Label() { Text = "Comment: ", Location = new System.Drawing.Point(10, 160), TextAlign=System.Drawing.ContentAlignment.MiddleCenter, AutoSize = true });
            TextBox textBoxComment = new TextBox() { Location = new System.Drawing.Point(80, 160), Size = new System.Drawing.Size(400, 10) };

            TabControl tc = new TabControl() { Location = new System.Drawing.Point(10, 50), Margin = new Padding(10), Size = new System.Drawing.Size(520, 100) };

            TabPage tabPageDirectory = new TabPage() { Text = "Directory", Name = "tabPageDirector", Size = new System.Drawing.Size(500, 200), UseVisualStyleBackColor = true, Tag = SyncType.Directory };
            tabPageDirectory.Controls.Add(new Label() { Text = "Directory Location:", Location = new System.Drawing.Point(10, 10), AutoSize = true });
            TextBox textBoxDirectory = new TextBox() { Location = new System.Drawing.Point(10, 30), Size = new System.Drawing.Size(450, 15) };
            var buttonDirectory = new System.Windows.Forms.Button() { Text = "...", Location = new System.Drawing.Point(470, 29), Size = new System.Drawing.Size(25, 25) };
            buttonDirectory.Click += (a, b) =>
            {
                FolderBrowserDialog dialog = new FolderBrowserDialog();
                dialog.ShowNewFolderButton = false;

                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    textBoxDirectory.Text = dialog.SelectedPath;
                }
            };

            tabPageDirectory.Controls.Add(textBoxDirectory);
            tabPageDirectory.Controls.Add(buttonDirectory);

            TabPage tabPageFile = new TabPage() { Text = "File", Name = "tabPageFile", Size = new System.Drawing.Size(500, 200), UseVisualStyleBackColor = true, Tag = SyncType.File };
            tabPageFile.Controls.Add(new Label() { Text = "File Location:", Location = new System.Drawing.Point(10, 10), AutoSize = true });
            TextBox textBoxFile = new TextBox() { Location = new System.Drawing.Point(10, 30), Size = new System.Drawing.Size(450, 15) };
            var buttonFile = new System.Windows.Forms.Button() { Text = "...", Location = new System.Drawing.Point(470, 29), Size = new System.Drawing.Size(25, 25) };
            buttonFile.Click += (a, b) =>
            {
                OpenFileDialog dialog = new OpenFileDialog();
                dialog.RestoreDirectory = true;
                dialog.Multiselect = false;

                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    textBoxFile.Text = dialog.FileName;
                }
            };

            tabPageFile.Controls.Add(textBoxFile);
            tabPageFile.Controls.Add(buttonFile);

            TabPage tabPageReg = new TabPage() { Text = "Registry Key", Name = "tabPageReg", Size = new System.Drawing.Size(500, 200), UseVisualStyleBackColor = true, Tag = SyncType.RegistryKey };
            tabPageReg.Controls.Add(new Label() { Text = "Regisry Key Path:", Location = new System.Drawing.Point(10, 10), AutoSize = true });
            TextBox textBoxReg = new TextBox() { Location = new System.Drawing.Point(10, 30), Size = new System.Drawing.Size(490, 15) };

            tabPageReg.Controls.Add(textBoxReg);

            tc.Controls.Add(tabPageDirectory);
            tc.Controls.Add(tabPageFile);
            tc.Controls.Add(tabPageReg);

            var okButton = new System.Windows.Forms.Button() { Text = "OK", Location = new System.Drawing.Point(430, 200), Size = new System.Drawing.Size(100, 30), Margin = new Padding(10) };
            okButton.Click += (a, b) =>
            {
                if (cb.SelectedIndex >= 0)
                {
                    SyncType type = (SyncType)tc.SelectedTab.Tag;
                    string source = null;

                    if (type == SyncType.Directory)
                    {
                        if (Directory.Exists(textBoxDirectory.Text))
                        {
                            source = textBoxDirectory.Text.Trim(new char[] { '\"', '\\' });
                        }
                        else
                            MessageBox.Show("Given directory does not exist!", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                    else if (type == SyncType.File)
                    {
                        if (File.Exists(textBoxFile.Text))
                        {
                            source = textBoxFile.Text.Trim(new char[] { '\"', '\\' }); ;
                        }
                        else
                            MessageBox.Show("Given file does not exist!", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                    else if (type == SyncType.RegistryKey)
                    {
                        RegistryKey sourceKey = RegistryHelper.GetKey(textBoxReg.Text.Trim(new char[] { '\"', '\\' }));
                        if (sourceKey != null)
                        {
                            source = textBoxReg.Text.Trim(new char[] { '\"', '\\' });
                        }
                        else
                            MessageBox.Show("Given registry key does not exist!", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }

                    if (!string.IsNullOrEmpty(source))
                        if (dbManager.AddSyncDefinition(games[cb.SelectedIndex].ID, source, type, textBoxComment.Text))
                        {
                            MessageBox.Show("Sync defintion added successfully.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                            f.Close();
                        }
                        else MessageBox.Show("Error adding sync definition.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    else
                        MessageBox.Show("Error parsing source string.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);

                }
                else MessageBox.Show("No game selected!", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            };

            var cancelButton = new System.Windows.Forms.Button() { Text = "Cancel", Location = new System.Drawing.Point(320, 200), Size = new System.Drawing.Size(100, 30), Margin = new Padding(10) };
            cancelButton.Click += (a, b) => { f.Close(); };

            f.Controls.Add(cb);
            f.Controls.Add(tc);
            f.Controls.Add(okButton);
            f.Controls.Add(cancelButton);
            f.Controls.Add(textBoxComment);

            f.ShowDialog();
        }

        private void addGameButtonAction()
        {
            Form f = new Form();
            f.Icon = new System.Drawing.Icon("Media/swappy_icon.ico");
            f.Text = "Add Game";
            f.Size = new System.Drawing.Size(670, 200);
            f.StartPosition = FormStartPosition.CenterParent;
            f.FormBorderStyle = FormBorderStyle.FixedSingle;

            TextBox textBoxName = new TextBox() { Size = new System.Drawing.Size(500, 12) };
            TextBox textBoxExec = new TextBox() { Size = new System.Drawing.Size(500, 12) };
            TextBox textBoxArgs = new TextBox() { Size = new System.Drawing.Size(500, 12) };

            var fileButton = new System.Windows.Forms.Button() { Text="...", Size = new System.Drawing.Size(25, 25), Location = new System.Drawing.Point(610, 40) };
            fileButton.Click += (a,b) => 
            { 
                OpenFileDialog fileDialog = new OpenFileDialog();
                fileDialog.Filter = "Executable files (*.exe, *.bat)|*.exe;*.bat|All Files|*.*";
                fileDialog.RestoreDirectory = true;

                if(fileDialog.ShowDialog() == DialogResult.OK)
                {
                    textBoxExec.Text = fileDialog.FileName;
                }
            };

            f.Controls.Add(new Label() { Text = "Name", Location = new System.Drawing.Point(10,10), Size = new System.Drawing.Size(80, 15), TextAlign = System.Drawing.ContentAlignment.MiddleRight});
            f.Controls.Add(new Label() { Text = "Executable Location", Location = new System.Drawing.Point(10,40), Size = new System.Drawing.Size(80, 15), TextAlign = System.Drawing.ContentAlignment.MiddleRight});
            f.Controls.Add(new Label() { Text = "Arguments", Location = new System.Drawing.Point(10,70), Size = new System.Drawing.Size(80, 15), TextAlign = System.Drawing.ContentAlignment.MiddleRight });

            textBoxName.Location = new System.Drawing.Point(100, 10);
            textBoxExec.Location = new System.Drawing.Point(100, 40);
            textBoxArgs.Location = new System.Drawing.Point(100, 70);

            var okButton = new System.Windows.Forms.Button() { Text="OK", Size = new System.Drawing.Size(100, 30), Location = new System.Drawing.Point(f.Size.Width - 130, f.Size.Height - 90) };
            okButton.Click += (a, b) => 
            { 
                if(!string.IsNullOrEmpty(textBoxName.Text) && !string.IsNullOrEmpty(textBoxExec.Text))
                {
                    try
                    {
                        if (string.IsNullOrEmpty(textBoxArgs.Text))
                            dbManager.AddGame(textBoxName.Text, textBoxExec.Text);
                        else
                            dbManager.AddGame(textBoxName.Text, textBoxExec.Text, textBoxArgs.Text);

                        MessageBox.Show("Game added successfully.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        f.Close();
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("An error occurred: \n" + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
                else
                {
                    if (string.IsNullOrEmpty(textBoxName.Text))
                        MessageBox.Show("Name field cannot be empty!", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    else if (string.IsNullOrEmpty(textBoxExec.Text))
                        MessageBox.Show("Executable field cannot be empty!", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            };

            var cancelButton = new System.Windows.Forms.Button() { Text="Cancel", Size=new System.Drawing.Size(100, 30), Location = new System.Drawing.Point(f.Size.Width - 250, f.Size.Height - 90) };
            cancelButton.Click += (a, b) => { f.Close(); };

            f.Controls.Add(textBoxName);
            f.Controls.Add(textBoxExec);
            f.Controls.Add(textBoxArgs);

            f.Controls.Add(fileButton);
            f.Controls.Add(okButton);
            f.Controls.Add(cancelButton);

            f.ShowDialog();
        }
    }
}
