using Microsoft.Win32;
using save_switcher.Elements;
using SharpDX;
using SharpDX.Direct2D1;
using SharpDX.DirectWrite;
using SharpDX.Mathematics.Interop;
using SharpDX.Multimedia;
using SharpDX.WIC;
using SharpDX.XAudio2;
using System;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;

namespace save_switcher.Panels
{
    internal class RunGame : Panel
    {
        private const float baseFinalizeButtonSize = 200;
        private const float baseBorderThickness = 5f;

        private DeviceContext deviceContext;

        private Size2 baseScalingResolution = new Size2(1920, 1080);

        private (int userId, int gameId) inputs;

        Game game;
        User user;
        DatabaseManager databaseManager;

        private Ellipse finalizeButton;
        private float finalizeButtonSize;
        private float borderThickness;
        private (float scale, bool isMouseOver) finalizeButtonProperties;

        private BitmapImage checkmarkImage;

        private float maxScale = 1.2f;

        private LinearGradientBrush backgroundGradientBrush;
        private SolidColorBrush colorBrush;

        private TextLayout waitTextLayout;
        private TextLayout pressButtonLayout;

        private XAudio2 audioOut;
        private SoundStream profileClickStream;
        private AudioBuffer selectionClickBuffer;
        private SourceVoice selectionClickVoice;

        private Stopwatch sw;
        private long lastMilliseconds;

        private bool gameProcessExited = false;

        public override void Initialize(DeviceContext deviceContext, params object[] args)
        {
            int? gameId = args[0] as int?;
            int userId = (int)args[1];

            if(!gameId.HasValue)
                throw new ArgumentNullException(nameof(gameId));


            databaseManager = new DatabaseManager();

            sw = new Stopwatch();

            this.deviceContext = deviceContext;

            inputs.userId = userId;
            inputs.gameId = gameId.Value;
            user = databaseManager.GetUser(userId);
            game = databaseManager.GetGame(gameId.Value);

            //set up audio effects
            audioOut = new XAudio2();
            MasteringVoice voice = new MasteringVoice(audioOut);

            profileClickStream = new SoundStream(File.OpenRead("Media/profile_selection_2.wav"));

            selectionClickBuffer = new AudioBuffer
            {
                Stream = profileClickStream.ToDataStream(),
                AudioBytes = (int)profileClickStream.Length,
            };
            profileClickStream.Close();


            colorBrush = new SolidColorBrush(deviceContext, Color.White);

            finalizeButtonProperties = (1f, false);

            ImagingFactory imagingFactory = new ImagingFactory();
            checkmarkImage = new BitmapImage("Media/checkmark.png", deviceContext, imagingFactory);

            createSizeDependantResources(deviceContext);

            //copy save to game location
            if (!Equals(user, null))
            {
                if (!Equals(game, null))
                {
                    Sync[] syncs = databaseManager.GetUserSyncs(game.ID, user.ID);

                    foreach (Sync sync in syncs)
                    {
                        if (sync.Type == SyncType.Directory)
                        {
                            //is directory
                            //Directory.Delete(sync.Destination, true);
                            string tempDirectory = sync.GameLocation.Trim('\\') + "_temp";
                            if (!Directory.Exists(tempDirectory))
                                Directory.Move(sync.GameLocation, tempDirectory);

                            Directory.CreateDirectory(sync.GameLocation);

                            copyDirectory(sync.ApplicationLocation, sync.GameLocation);
                        }
                        else if (sync.Type == SyncType.File)
                        {
                            //is file
                            File.Move(sync.GameLocation, sync.GameLocation.Trim('\\') + "_temp");

                            if (sync.ApplicationLocation != null)
                            {
                                File.Copy(sync.ApplicationLocation, sync.GameLocation, true);
                                Console.WriteLine($"Copying {sync.ApplicationLocation} --> {sync.GameLocation}");
                            }

                        }
                        else if (sync.Type == SyncType.RegistryKey)
                        {
                            RegistryKey key = RegistryHelper.GetKey(sync.GameLocation);

                            if (key != null)
                            {
                                RegistryKey tempKey = RegistryHelper.GetKey(sync.GameLocation + "_temp");

                                if (tempKey != null)
                                    RegistryHelper.DeleteRegistryKey(tempKey);

                                RegistryHelper.CreateRegistryKey(sync.GameLocation + "_temp");

                                RegistryHelper.CopyRegistryKey(key, RegistryHelper.GetKey(sync.GameLocation + "_temp"));

                                RegistryHelper.DeleteRegistryKey(key);

                                RegistryHelper.CreateRegistryKey(sync.GameLocation);

                                key = RegistryHelper.GetKey(sync.GameLocation);
                            }
                            else
                            {
                                RegistryHelper.CreateRegistryKey(sync.GameLocation);
                                key = RegistryHelper.GetKey(sync.GameLocation);
                            }

                            if (Directory.Exists(sync.ApplicationLocation))
                            {
                                foreach (string file in Directory.GetFiles(sync.ApplicationLocation))
                                {
                                    RegistryHelper.ReadRegistryFromFile(key, new FileStream(file, FileMode.Open, FileAccess.Read));
                                }
                            }
                        }
                    }

                    if (syncs.Length == 0)
                        Console.WriteLine("No syncs found, starting...");

                    Process proc = new Process();
                    proc.StartInfo.FileName = game.Exec;
                    proc.EnableRaisingEvents = true;
                    proc.StartInfo.WorkingDirectory = Path.GetDirectoryName(game.Exec);

                    if (!Equals(game.Args, null))
                        proc.StartInfo.Arguments = game.Args;

                    proc.Start();

                    proc.Exited += (_,__) => 
                    {
                        gameProcessExited = true;

                        Program.ActivateForm();
                    };

                }
                else
                {
                    //game does not exist
                    MessageBox.Show($"Error: Game {inputs.gameId} does not exist!", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    Application.Exit();
                }

            }
            else
            {
                //user does not exist
                MessageBox.Show($"Error: User {inputs.userId} does not exist!", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Application.Exit();
            }

            InputManager.OnEnterInput += inputEnter;
            InputManager.OnMousePosChanged += inputMousePosChanged;
            InputManager.OnLeftMouseInput += inputLeftMouseInput;
        }

        private void inputEnter(InputManager.ButtonTravel travel)
        {
            if(gameProcessExited)
                finalize();
        }

        private void inputLeftMouseInput(InputManager.ButtonTravel travel)
        {
            if (gameProcessExited && finalizeButtonProperties.isMouseOver)
                finalize();
        }

        private void inputMousePosChanged(System.Drawing.Point p)
        {
            Vector2 mousePos = new Vector2(p.X, p.Y);
            if (Vector2.DistanceSquared(mousePos, new Vector2(finalizeButton.Point.X, finalizeButton.Point.Y)) < finalizeButtonSize / 2 * finalizeButtonProperties.scale * (finalizeButtonSize / 2 * finalizeButtonProperties.scale))
            {
                if (!finalizeButtonProperties.isMouseOver)
                    playSelectedSound();

                finalizeButtonProperties.isMouseOver = true;
            }
            else
                finalizeButtonProperties.isMouseOver = false;

        }

        private void createSizeDependantResources(DeviceContext deviceContext)
        {
            float sizeScaling = deviceContext.Size.Height / baseScalingResolution.Height * (deviceContext.Size.Height / baseScalingResolution.Height / (deviceContext.Size.Width / baseScalingResolution.Width));

            finalizeButtonSize = baseFinalizeButtonSize * sizeScaling;
            borderThickness = baseBorderThickness * sizeScaling;

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
            backgroundGradientBrush = new LinearGradientBrush(deviceContext, backGroundGradientProperties, new GradientStopCollection(deviceContext, backgroundGradientStops, ExtendMode.Wrap));

            SharpDX.DirectWrite.Factory directWriteFactory = new SharpDX.DirectWrite.Factory();
            CustomFontCollectionLoader customFontLoader = new CustomFontCollectionLoader(directWriteFactory);
            FontCollection fontCollection = new FontCollection(directWriteFactory, customFontLoader, customFontLoader.KeyStream);

            TextFormat textFormat = new TextFormat(directWriteFactory, "Gabarito", fontCollection, FontWeight.Normal, FontStyle.Normal, FontStretch.Normal, 50f * sizeScaling);
            waitTextLayout = new TextLayout(directWriteFactory, "Waiting for your game to close...", textFormat, 1000f * sizeScaling, 200f * sizeScaling);

            waitTextLayout.ParagraphAlignment = ParagraphAlignment.Center;
            waitTextLayout.TextAlignment = TextAlignment.Center;

            pressButtonLayout = new TextLayout(directWriteFactory, "Press this button after your game closes.\nOtherwise it will not be saved!", textFormat, 1000f * sizeScaling, 200f * sizeScaling);
            pressButtonLayout.ParagraphAlignment = ParagraphAlignment.Center;
            pressButtonLayout.TextAlignment = TextAlignment.Center;

            finalizeButton = new Ellipse(new Vector2(deviceContext.Size.Width / 2, deviceContext.Size.Height / 4 * 3), finalizeButtonSize / 2, finalizeButtonSize / 2);
        }

        private void playSelectedSound()
        {
            selectionClickVoice = new SourceVoice(audioOut, profileClickStream.Format, false);
            selectionClickVoice.SubmitSourceBuffer(selectionClickBuffer, profileClickStream.DecodedPacketsInfo);
            selectionClickVoice.SetVolume(0.5f);
            selectionClickVoice.Start();
        }

        private float lerp(float start, float end, float amount)
        {
            if (amount < 0)
                return start;
            else if (amount > 1)
                return end;
            else
                return start + (end - start) * amount;

        }

        private void copyDirectory(string sourceDir, string destDir)
        {
            if (!Directory.Exists(sourceDir))
                return;

            if (destDir.Contains(sourceDir))
                return;

            if (!Directory.Exists(destDir))
                Directory.CreateDirectory(destDir);

            string[] files = Directory.GetFiles(sourceDir);

            foreach (string file in files)
            {
                File.Copy(file, destDir + $@"\{Path.GetFileName(file)}", true);
            }

            string[] dirs = Directory.GetDirectories(sourceDir);

            foreach (string directory in dirs)
            {
                copyDirectory(directory, $@"{destDir}\{Path.GetFileName(directory)}");
            }
        }

        public override void Resize(DeviceContext deviceContext)
        {
            this.deviceContext = deviceContext;

            createSizeDependantResources(deviceContext);
        }

        private void finalize()
        {
            Sync[] syncs2 = databaseManager.GetUserSyncs(game.ID, user.ID);

            foreach (Sync sync in syncs2)
            {
                if (sync.Type == SyncType.Directory)
                {
                    if (Directory.Exists(sync.ApplicationLocation))
                        Directory.Move(sync.ApplicationLocation, $"{sync.ApplicationLocation}_{DateTime.UtcNow.Ticks}");

                    copyDirectory(sync.GameLocation, sync.ApplicationLocation);

                    Directory.Delete(sync.GameLocation, true);
                    Directory.Move(sync.GameLocation.Trim('\\') + "_temp", sync.GameLocation);
                }
                else if (sync.Type == SyncType.File)
                {
                    if (File.Exists(sync.ApplicationLocation))
                        File.Move(sync.ApplicationLocation, $"{sync.ApplicationLocation}_{DateTime.UtcNow.Ticks}");

                    File.Copy(sync.GameLocation, sync.ApplicationLocation, false);

                    File.Delete(sync.GameLocation);
                    File.Move(sync.GameLocation.Trim('\\') + "_temp", sync.GameLocation);
                }
                else if (sync.Type == SyncType.RegistryKey)
                {
                    if (Directory.Exists(sync.ApplicationLocation))
                        Directory.Move(sync.ApplicationLocation, $"{sync.ApplicationLocation}_{DateTime.UtcNow.Ticks}");

                    if (!Directory.Exists(sync.ApplicationLocation))
                        Directory.CreateDirectory(sync.ApplicationLocation);

                    RegistryHelper.WriteRegistryKeyToFile(RegistryHelper.GetKey(sync.GameLocation), sync.ApplicationLocation);

                    if (RegistryHelper.GetKey(sync.GameLocation) != null)
                    {
                        RegistryHelper.DeleteRegistryKey(RegistryHelper.GetKey(sync.GameLocation));

                        if (RegistryHelper.GetKey(sync.GameLocation + "_temp") != null)
                        {
                            RegistryHelper.CreateRegistryKey(sync.GameLocation);
                            RegistryHelper.CopyRegistryKey(RegistryHelper.GetKey(sync.GameLocation + "_temp"), RegistryHelper.GetKey(sync.GameLocation));

                            RegistryHelper.DeleteRegistryKey(RegistryHelper.GetKey(sync.GameLocation + "_temp"));
                        }
                    }
                }

                int syncDefId = databaseManager.GetSyncDefID(game.ID, sync.GameLocation, sync.Type);

                if (syncDefId >= 0)
                    databaseManager.UpdateUserSync(user.ID, syncDefId);
            }

            Application.Exit();
        }

        public override void Draw(DeviceContext deviceContext)
        {
            if (!sw.IsRunning)
            {
                sw.Start();
                lastMilliseconds = sw.ElapsedMilliseconds;
            }

            float lerpAmount = (sw.ElapsedMilliseconds - lastMilliseconds) / 1000f * 50f;

            finalizeButtonProperties.scale = lerp(finalizeButtonProperties.scale, finalizeButtonProperties.isMouseOver ? maxScale : 1f, lerpAmount);

            deviceContext.Transform = Matrix3x2.Identity;
            deviceContext.BeginDraw();

            deviceContext.Clear(Color.Black);

            deviceContext.FillRectangle(new RawRectangleF(0, 0, deviceContext.Size.Width, deviceContext.Size.Height), backgroundGradientBrush);

            if (!gameProcessExited)
            {
                deviceContext.DrawTextLayout(new RawVector2(deviceContext.Size.Width / 2 - waitTextLayout.MaxWidth / 2, deviceContext.Size.Height / 2 - waitTextLayout.MaxHeight / 2), waitTextLayout, colorBrush);
            }
            else
            {
                deviceContext.DrawTextLayout(new RawVector2(deviceContext.Size.Width / 2 - pressButtonLayout.MaxWidth / 2, deviceContext.Size.Height / 2 - pressButtonLayout.MaxHeight / 2), pressButtonLayout, colorBrush);

                deviceContext.Transform = Matrix3x2.Multiply(Matrix3x2.Scaling(finalizeButtonProperties.scale, finalizeButtonProperties.scale, new Vector2(finalizeButton.Point.X, finalizeButton.Point.Y)), deviceContext.Transform);
                colorBrush.Color = Color.LightGreen;
                deviceContext.FillEllipse(finalizeButton, colorBrush);

                deviceContext.DrawBitmap(checkmarkImage.Image, new RawRectangleF(finalizeButton.Point.X - finalizeButton.RadiusX * 0.7f, finalizeButton.Point.Y - finalizeButton.RadiusY * 0.7f, finalizeButton.Point.X + finalizeButton.RadiusX * 0.7f, finalizeButton.Point.Y + finalizeButton.RadiusY * 0.7f), 1f, SharpDX.Direct2D1.BitmapInterpolationMode.Linear);

                colorBrush.Color = Color.White;
                deviceContext.StrokeWidth = borderThickness;
                deviceContext.DrawEllipse(finalizeButton, colorBrush);
            }
            

            deviceContext.EndDraw();

  
            lastMilliseconds = sw.ElapsedMilliseconds;
        }
    }
}
