using SharpDX.Direct2D1;
using SharpDX;
using SharpDX.Mathematics.Interop;
using save_switcher.Elements;
using SharpDX.WIC;
using SharpDX.DirectWrite;
using System;
using System.Windows.Forms;
using System.IO;
using System.Diagnostics;
using SharpDX.XAudio2;
using SharpDX.Multimedia;
using SharpDX.XInput;
using System.Collections.Generic;
using SharpDX.Direct2D1.Effects;

namespace save_switcher.Panels
{
    internal class AddUser : Panel, Mouseable, Keyboardable
    {
        private const float baseProfilePictureSize = 400;
        private const float baseDeleteButtonSize = 150;
        private const float baseConfirmDeletePanelImageSize = 100;
        private const float baseChangePanelButtonSize = 150;
        private const float baseUsernameTextboxWidth = 700f;
        private const float baseBorderThickness = 5f;
        private const float baseErrorIconSize = 50f;
        private readonly Size2 baseScalingResolution = new Size2(1920, 1080);

        private readonly Program.ChangePanel changePanelCallback;
        private DeviceContext deviceContext;
        private float currentDeviceScale;

        private const float selectedScale = 1.2f;
        private float borderThickness;

        private Stopwatch sw;
        private float lastMilliseconds;
        private long lastControllerReconnectTime;

        private Controller[] controllers;
        private State[] oldControllerState;
        private readonly float deadZone = 0.8f;
        private Vector2 oldMousePos;
        private SelectableElement currentSelected = SelectableElement.None;

        private XAudio2 audioOut;
        private SoundStream profileClickStream;
        private AudioBuffer selectionClickBuffer;
        private SourceVoice selectionClickVoice;

        private float profilePictureSize;
        private EllipseGeometry profileGeometry;
        private EllipseGeometry defaultImageGeometry;
        private EllipseGeometry profileImageGeometry;

        private LinearGradientBrush backgroundGradientBrush;
        private SolidColorBrush colorBrush;

        private readonly BitmapImage defaultProfileImage;
        private BitmapImage profileImage;
        private ImageBrush profileBrush;
        private ImagingFactory imagingFactory;

        
        private float deleteButtonSize;
        private BitmapImage deleteImage;
        private EllipseGeometry deleteGeometry;

        private BitmapImage errorImage;
        private float confirmDeletePanelImageSize;
        private float errorIconSize;
        private ImageBrush confirmDeletePanelImageBrush;
        private Effect errorIconTinted;
        private RoundedRectangleGeometry confirmDeletePanel;
        private RoundedRectangleGeometry confirmDeleteOKButton;
        private RoundedRectangleGeometry confirmDeleteCancelButton;
        private TextLayout confirmDeleteText;
        private TextLayout confirmDeleteOKText;
        private TextLayout confirmDeleteCancelText;

        private TextLayout errorProfileLayout;
        private TextLayout errorUsernameLayout;

        private float changePanelButtonSize;
        private BitmapImage acceptButtonImage;
        private BitmapImage cancelButtonImage;
        private EllipseGeometry acceptButtonGeometry;
        private EllipseGeometry cancelButtonGeometry;

        private string profileFullFileName;

        private SharpDX.DirectWrite.Factory directWriteFactory;
        private CustomFontCollectionLoader customFontLoader;
        private FontCollection fontCollection;
        private TextFormat usernameTextFormat;
        private TextLayout usernameLayout;
        private string username;
        private const int usernameLimit = 15;
        private TextLayout defaultUsernameLayout;

        private (float profilePicture, float acceptButton, float cancelButton, float deleteButton, float usernameTextbox,
            float confirmDeleteYes, float confirmDeleteNo) currentScales;

        private bool isShowingConfirmDelete;
        private readonly bool isEditingUserFlag;
        private readonly int? editingUserID;

        private (bool leftMouseReleased, bool leftMousePressed) mouseState;

        private (bool profile, bool username) showEmptyError = (false, false);

        private RoundedRectangle usernameTextboxRect;
        private float usernameTextboxWidth = 700f;

        private enum InputChoices
        {
            Enter,
            Cancel,
            Left,
            Right,
            Up,
            Down,
        }

        private enum SelectableElement
        {
            None,
            Accept,
            Cancel,
            Textbox,
            Profile,
            Delete,
            ConfirmDeleteYes,
            ConfirmDeleteNo,
        }

        public AddUser(DeviceContext deviceContext, Program.ChangePanel changePanelCallback, User editUser = null) 
        {
            sw = new Stopwatch();

            reconnectControllers();

            mouseState = (false, false);

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


            this.changePanelCallback = changePanelCallback;
            this.deviceContext = deviceContext;

            currentScales = (1f, 1f, 1f, 1f, 1f, 1f, 1f);


            colorBrush = new SolidColorBrush(deviceContext, Color.Black);

            imagingFactory = new ImagingFactory();

            acceptButtonImage = new BitmapImage("Media/checkmark.png", deviceContext, imagingFactory);
           
            cancelButtonImage = new BitmapImage("Media/close.png", deviceContext, imagingFactory);

            errorImage = new BitmapImage("Media/warning.png", deviceContext, imagingFactory);

            errorIconTinted = new Effect(deviceContext, Effect.Tint);
            errorIconTinted.InputCount = 1;
            errorIconTinted.SetValue(0, new RawColor4(1f, 0.28f, 0.27f, 1f));
            errorIconTinted.SetInput(0, errorImage.Image, false);


            if (editUser == null)
            {
                defaultProfileImage = new BitmapImage("Media/default_user_profile.png", deviceContext, imagingFactory);


                ImageBrushProperties imageBrushProperties = new ImageBrushProperties()
                {
                    ExtendModeX = ExtendMode.Clamp,
                    ExtendModeY = ExtendMode.Clamp,
                    InterpolationMode = InterpolationMode.Linear,
                    SourceRectangle = new RawRectangleF(0f, 0f, defaultProfileImage.Image.Size.Width, defaultProfileImage.Image.Size.Height),
                };

                profileBrush = new ImageBrush(deviceContext, defaultProfileImage.Image, imageBrushProperties);
                float defaultProfileBrushRadius = defaultProfileImage.Image.Size.Width / 2;
                defaultImageGeometry = new EllipseGeometry(deviceContext.Factory, new Ellipse(new Vector2(defaultProfileBrushRadius, defaultProfileBrushRadius), defaultProfileBrushRadius, defaultProfileBrushRadius));
            }
            else
            {
                isEditingUserFlag = true;
                editingUserID = editUser.ID;

                username = editUser.Username;

                string profileImageFile = "";
                string userProfileImage = $@"syncs\user_data\{editUser.ID}\profile.png";

                if (!File.Exists(userProfileImage))
                    profileImageFile = "Media/default_user_profile.png";
                else
                    profileImageFile = userProfileImage;

                profileFullFileName = Path.GetFullPath(profileImageFile);

                profileImage = new BitmapImage(profileFullFileName, deviceContext, imagingFactory);

                ImageBrushProperties imageBrushProperties1 = new ImageBrushProperties()
                {
                    ExtendModeX = ExtendMode.Clamp,
                    ExtendModeY = ExtendMode.Clamp,
                    InterpolationMode = InterpolationMode.Linear,
                    SourceRectangle = new RawRectangleF(0f, 0f, profileImage.Image.Size.Width, profileImage.Image.Size.Height),
                };

                profileBrush = new ImageBrush(deviceContext, profileImage.Image, imageBrushProperties1);
                profileBrush.Transform = Matrix3x2.Scaling(1f, profileImage.Image.Size.Width / profileImage.Image.Size.Height);

                float profileBrushRadius = profileImage.Image.Size.Width / 2;
                profileImageGeometry = new EllipseGeometry(deviceContext.Factory, new Ellipse(new Vector2(profileBrushRadius, profileBrushRadius), profileBrushRadius, profileBrushRadius));

                deleteImage = new BitmapImage("Media/delete.png", deviceContext, imagingFactory);
            }

            createSizeDependantResources(deviceContext);
        }

        public void OnMouseDown(System.Windows.Forms.MouseEventArgs e)
        {
            if (System.Windows.Forms.Form.ActiveForm == null)
                return;

            mouseState.leftMousePressed = true;
        }

        public void OnMouseUp(System.Windows.Forms.MouseEventArgs e) 
        {
            if(e.Button != MouseButtons.Left) 
                return;

            if(mouseState.leftMousePressed)
                mouseState.leftMouseReleased = true;

            mouseState.leftMousePressed = false;
        }

        public void OnMouseWheel(System.Windows.Forms.MouseEventArgs e) { }

        public void OnKeyDown(System.Windows.Forms.KeyEventArgs e) 
        {
            
            if (username == null)
                username = "";

            if (username.Length > 0 && e.KeyValue == (int)System.Windows.Forms.Keys.Back)
            {
                if (e.Control)
                {
                    string[] splitUsername = username.Split(' ');
                    string[] destSplitUsername = new string[splitUsername.Length - 1];
                    Array.Copy(splitUsername, destSplitUsername, splitUsername.Length - 1);

                    username = string.Join(" ", destSplitUsername);
                }
                else
                    username = username.Substring(0, username.Length - 1);

                usernameLayout.Dispose();
                usernameLayout = new TextLayout(directWriteFactory, username, usernameTextFormat, 1000f, 100f);
            }

            if(e.KeyValue == (int)Keys.Enter)
            {
                input(InputChoices.Enter);
            }

            if(e.KeyValue == (int)Keys.Escape)
            {
                input(InputChoices.Cancel);
            }

            if(e.KeyValue == (int)Keys.Left)
            {
                input(InputChoices.Left);
            }

            if(e.KeyValue == (int)Keys.Right)
            {
                input(InputChoices.Right);
            }

            if(e.KeyValue == (int)Keys.Up)
            {
                input(InputChoices.Up);
            }

            if(e.KeyValue == (int)Keys.Down)
            {
                input(InputChoices.Down);
            }
        }

        public void OnKeyUp(System.Windows.Forms.KeyEventArgs e) { }

        public void OnKeyPress(System.Windows.Forms.KeyPressEventArgs e)
        {
            
            if (username == null)
                username = "";

            int asciiCode = e.KeyChar;

            //filters uppercase, lowercase, numbers, ., -, (, ) and 'SPACE'
            if ((asciiCode >= 65 && asciiCode <= 90) || (asciiCode >= 97 && asciiCode <= 122) || (asciiCode >= 48 && asciiCode <= 57) || asciiCode == 45 || asciiCode == 46 || asciiCode == 40 || asciiCode == 41 || asciiCode == 32)
            {
                if (username.Length + 1 <= usernameLimit)
                    username = username + e.KeyChar;

                showEmptyError.username = false;
            }

            usernameLayout.Dispose();
            usernameLayout = new TextLayout(directWriteFactory, username, usernameTextFormat, 1000f, 100f);
        }

        private void createSizeDependantResources(DeviceContext deviceContext)
        {
            float sizeScaling = deviceContext.Size.Height / baseScalingResolution.Height * (deviceContext.Size.Height / baseScalingResolution.Height / (deviceContext.Size.Width / baseScalingResolution.Width));

            Console.WriteLine(sizeScaling);
            profilePictureSize = baseProfilePictureSize * sizeScaling;
            deleteButtonSize = baseDeleteButtonSize * sizeScaling;
            confirmDeletePanelImageSize = baseConfirmDeletePanelImageSize * sizeScaling;
            changePanelButtonSize = baseChangePanelButtonSize * sizeScaling;
            usernameTextboxWidth = baseUsernameTextboxWidth * sizeScaling;
            borderThickness = baseBorderThickness * sizeScaling;
            currentDeviceScale = sizeScaling;
            errorIconSize = baseErrorIconSize * sizeScaling;

            usernameTextboxRect = new RoundedRectangle()
            {
                RadiusX = 75 / 2 * sizeScaling,
                RadiusY = 75 / 2 * sizeScaling,
                Rect = new RawRectangleF(deviceContext.Size.Width / 2 - (usernameTextboxWidth / 2), deviceContext.Size.Height / 2, deviceContext.Size.Width / 2 + (usernameTextboxWidth / 2), deviceContext.Size.Height / 2 + 75 * sizeScaling),
            };

            LinearGradientBrushProperties gradientProperties = new LinearGradientBrushProperties()
            {
                StartPoint = new Vector2(0f, 0f),
                EndPoint = new Vector2(deviceContext.Size.Width, deviceContext.Size.Height),
            };

            GradientStop[] gradientStops2 = new GradientStop[]
            {
                new GradientStop()
                {
                    Color = new Color(92, 39, 116),
                    Position = 0f,
                },
                new GradientStop()
                {
                    Color = new Color(51, 92, 197),
                    Position = 0.7f,
                },
                new GradientStop()
                {
                    Color = new Color(99, 127, 253),
                    Position = 0.9f,
                }
            };

            GradientStop[] gradientStops = new GradientStop[]
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
            GradientStopCollection gradientCollection = new GradientStopCollection(deviceContext, gradientStops, ExtendMode.Clamp);

            backgroundGradientBrush = new LinearGradientBrush(deviceContext, gradientProperties, gradientCollection);

            profileGeometry = new EllipseGeometry(deviceContext.Factory, new Ellipse(new Vector2(profilePictureSize / 2, profilePictureSize / 2), profilePictureSize / 2, profilePictureSize / 2));


            ImageBrushProperties imageBrushProperties = new ImageBrushProperties
            {
                SourceRectangle = new RawRectangleF(0f, 0f, errorImage.Image.Size.Width, errorImage.Image.Size.Height),
                InterpolationMode = InterpolationMode.Anisotropic
            };

            BrushProperties brushProperties = new BrushProperties
            {
                Transform = Matrix3x2.Scaling(confirmDeletePanelImageSize / errorImage.Image.Size.Width),
                Opacity = 1f
            };

            confirmDeletePanelImageBrush = new ImageBrush(deviceContext, errorIconTinted.Output, imageBrushProperties, brushProperties);


            directWriteFactory = new SharpDX.DirectWrite.Factory();
            customFontLoader = new CustomFontCollectionLoader(directWriteFactory);
            fontCollection = new FontCollection(directWriteFactory, customFontLoader, customFontLoader.KeyStream);

            usernameTextFormat = new TextFormat(directWriteFactory, "Gabarito", fontCollection, FontWeight.Normal, FontStyle.Normal, FontStretch.Normal, 75f * sizeScaling);

            TextFormat defaultUsernameTextFormat = new TextFormat(directWriteFactory, "Gabarito", fontCollection, FontWeight.Normal, FontStyle.Italic, FontStretch.Normal, 75f * sizeScaling);

            defaultUsernameLayout = new TextLayout(directWriteFactory, "Username", defaultUsernameTextFormat, 1000f * sizeScaling, 75f * sizeScaling);

            TextFormat errorTextFormat = new TextFormat(directWriteFactory, "Gabarito", fontCollection, FontWeight.Normal, FontStyle.Normal, FontStretch.Normal, 30f * sizeScaling);

            float getLayoutWidth(TextLayout layout)
            {
                float totalWidth = 0f;

                foreach (ClusterMetrics clusterMetrics in layout.GetClusterMetrics())
                    totalWidth += clusterMetrics.Width;

                return totalWidth;
            }

            errorProfileLayout = new TextLayout(directWriteFactory, "Profile picture cannot be empty!", errorTextFormat, 100f * sizeScaling, 25f * sizeScaling);
            errorProfileLayout.ParagraphAlignment = ParagraphAlignment.Center;
            errorProfileLayout.MaxWidth = getLayoutWidth(errorProfileLayout);

            errorUsernameLayout = new TextLayout(directWriteFactory, "Username cannot be empty!", errorTextFormat, 100f * sizeScaling, 25f * sizeScaling);
            errorUsernameLayout.ParagraphAlignment = ParagraphAlignment.Center;
            errorUsernameLayout.MaxWidth = getLayoutWidth(errorUsernameLayout);


            acceptButtonGeometry = new EllipseGeometry(deviceContext.Factory, new Ellipse(new Vector2(deviceContext.Size.Width / 2 - changePanelButtonSize, deviceContext.Size.Height / 4 * 3), changePanelButtonSize / 2, changePanelButtonSize / 2));

            cancelButtonGeometry = new EllipseGeometry(deviceContext.Factory, new Ellipse(new Vector2(deviceContext.Size.Width / 2 + changePanelButtonSize, deviceContext.Size.Height / 4 * 3), changePanelButtonSize / 2, changePanelButtonSize / 2));


            if(editingUserID == null)
            {
                usernameLayout = new TextLayout(directWriteFactory, "", usernameTextFormat, 100f * sizeScaling, 100f * sizeScaling);
            }
            else
            {
                usernameLayout = new TextLayout(directWriteFactory, username, usernameTextFormat, 1000f * sizeScaling, 100f * sizeScaling);

                deleteGeometry = new EllipseGeometry(deviceContext.Factory, new Ellipse(new Vector2(deviceContext.Size.Width - (50f * sizeScaling)  - deleteButtonSize / 2, (50f * sizeScaling) + deleteButtonSize / 2), deleteButtonSize / 2, deleteButtonSize / 2));

                confirmDeletePanel = new RoundedRectangleGeometry(deviceContext.Factory, new RoundedRectangle()
                {
                    Rect = new RawRectangleF(deviceContext.Size.Width / 2 - 400f * sizeScaling, deviceContext.Size.Height / 2 - 200f * sizeScaling, deviceContext.Size.Width / 2 + 400f * sizeScaling, deviceContext.Size.Height / 2 + 200f * sizeScaling),
                    RadiusX = 50f * sizeScaling,
                    RadiusY = 50f * sizeScaling
                });

                confirmDeleteCancelButton = new RoundedRectangleGeometry(deviceContext.Factory, new RoundedRectangle()
                {
                    Rect = new RawRectangleF(confirmDeletePanel.RoundedRect.Rect.Left + 50f * sizeScaling, confirmDeletePanel.RoundedRect.Rect.Bottom - 150f * sizeScaling, confirmDeletePanel.RoundedRect.Rect.Left + (confirmDeletePanel.RoundedRect.Rect.Right - confirmDeletePanel.RoundedRect.Rect.Left) / 2 - 25f * sizeScaling, confirmDeletePanel.RoundedRect.Rect.Bottom - 50f * sizeScaling),
                    RadiusX = 50f * sizeScaling,
                    RadiusY = 50f * sizeScaling
                });

                confirmDeleteOKButton = new RoundedRectangleGeometry(deviceContext.Factory, new RoundedRectangle()
                {
                    Rect = new RawRectangleF(confirmDeletePanel.RoundedRect.Rect.Left + (confirmDeletePanel.RoundedRect.Rect.Right - confirmDeletePanel.RoundedRect.Rect.Left) / 2 + 25f * sizeScaling, confirmDeletePanel.RoundedRect.Rect.Bottom - 150f * sizeScaling, confirmDeletePanel.RoundedRect.Rect.Right - 50f * sizeScaling, confirmDeletePanel.RoundedRect.Rect.Bottom - 50f * sizeScaling),
                    RadiusX = 50f * sizeScaling,
                    RadiusY = 50f * sizeScaling
                });

                TextFormat confirmDeleteFormat = new TextFormat(directWriteFactory, "Gabarito", fontCollection, FontWeight.Normal, FontStyle.Normal, FontStretch.Normal, 35f * sizeScaling);
                confirmDeleteText = new TextLayout(directWriteFactory, "Are you sure you want to delete this profile?", confirmDeleteFormat, confirmDeletePanel.RoundedRect.Rect.Right - confirmDeletePanel.RoundedRect.Rect.Left, 35f * sizeScaling);
                confirmDeleteText.ParagraphAlignment = ParagraphAlignment.Center;
                confirmDeleteText.TextAlignment = TextAlignment.Center;

                TextFormat confirmButtonFormat = new TextFormat(directWriteFactory, "Gabarito", fontCollection, FontWeight.Normal, FontStyle.Normal, FontStretch.Normal, 32f * sizeScaling);

                confirmDeleteOKText = new TextLayout(directWriteFactory, "Throw it away!", confirmButtonFormat, confirmDeleteOKButton.RoundedRect.Rect.Right - confirmDeleteOKButton.RoundedRect.Rect.Left, 25f * sizeScaling);
                confirmDeleteOKText.ParagraphAlignment = ParagraphAlignment.Center;
                confirmDeleteOKText.TextAlignment = TextAlignment.Center;

                confirmDeleteCancelText = new TextLayout(directWriteFactory, "Save it!", confirmButtonFormat, confirmDeleteCancelButton.RoundedRect.Rect.Right - confirmDeleteCancelButton.RoundedRect.Rect.Left, 25f * sizeScaling);
                confirmDeleteCancelText.ParagraphAlignment = ParagraphAlignment.Center;
                confirmDeleteCancelText.TextAlignment = TextAlignment.Center;
            }
        }

        private void input(InputChoices inputChoice, bool mouseInput = false)
        {
            if (!mouseInput && currentSelected.Equals(SelectableElement.None) && inputChoice != InputChoices.Cancel && !isShowingConfirmDelete)
            {
                currentSelected = SelectableElement.Accept;
                playSelectedSound();
                return;
            }

            SelectableElement oldSelected = currentSelected;

            switch(inputChoice)
            {
                case InputChoices.Enter:
                    if (!isShowingConfirmDelete)
                    {
                        if (currentSelected.Equals(SelectableElement.Accept))
                        {
                            playSelectedSound();

                            if (File.Exists(profileFullFileName) && !(string.IsNullOrEmpty(username) || string.IsNullOrWhiteSpace(username)))
                            {
                                DatabaseManager dbManager = new DatabaseManager();

                                if (isEditingUserFlag && dbManager.UpdateUser(editingUserID.Value, username))
                                {
                                    string userDirectory = $@"syncs\user_data\{editingUserID.Value}";

                                    if (!Directory.Exists(userDirectory))
                                        Directory.CreateDirectory(userDirectory);

                                    string destination = $@"{userDirectory.TrimEnd('\\')}\profile.png";

                                    SharpDX.WIC.ImagingFactory2 wicFactory = new ImagingFactory2();
                                    Stream stream = new FileStream(destination, FileMode.Create, FileAccess.ReadWrite);
                                    BitmapImage image = new BitmapImage(profileFullFileName, deviceContext, wicFactory);

                                    BitmapEncoder bitmapEncoder = new BitmapEncoder(wicFactory, ContainerFormatGuids.Png, stream);
                                    BitmapFrameEncode bitmapFrameEncode = new BitmapFrameEncode(bitmapEncoder);
                                    bitmapFrameEncode.Initialize();

                                    ImageEncoder imageEncoder = new ImageEncoder(wicFactory, deviceContext.Device);
                                    imageEncoder.WriteFrame(image.Image, bitmapFrameEncode, new ImageParameters(new SharpDX.Direct2D1.PixelFormat(SharpDX.DXGI.Format.R8G8B8A8_UNorm, AlphaMode.Premultiplied), 96f, 96f, 0f, 0f, (int)image.Image.Size.Width, (int)image.Image.Size.Height));

                                    bitmapFrameEncode.Commit();
                                    bitmapEncoder.Commit();
                                    stream.Flush();
                                    stream.Close();
                                    //if (Path.GetFullPath(profileFullFileName) != Path.GetFullPath(destination))
                                    //    File.Copy(profileFullFileName, destination, true);

                                    changePanelCallback(new ProfileSelector(deviceContext, changePanelCallback));
                                }
                                else if (!isEditingUserFlag)
                                {
                                    if (dbManager.AddUser(username))
                                    {
                                        User newUser = dbManager.GetUser(username);
                                        string userDirectory = $@"syncs\user_data\{newUser.ID}";

                                        if (!Directory.Exists(userDirectory))
                                            Directory.CreateDirectory(userDirectory);

                                        File.Copy(profileFullFileName, $@"{userDirectory.TrimEnd('\\')}\profile.png");

                                        changePanelCallback(new ProfileSelector(deviceContext, changePanelCallback));
                                    }
                                }
                            }
                            else
                            {
                                if (!File.Exists(profileFullFileName))
                                    showEmptyError.profile = true;

                                if (string.IsNullOrEmpty(username) || string.IsNullOrWhiteSpace(username))
                                    showEmptyError.username = true;
                            }
                        }
                        else if (currentSelected.Equals(SelectableElement.Cancel))
                        {
                            playSelectedSound();
                            changePanelCallback(new ProfileSelector(deviceContext, changePanelCallback));
                        }
                        else if (currentSelected.Equals(SelectableElement.Profile))
                        {
                            playSelectedSound();
                            OpenFileDialog dialog = new OpenFileDialog();

                            dialog.Filter = "Image Files (PNG, JPG & BMP)|*.png;*.jpg;*.jpeg;*.bmp;";

                            if (dialog.ShowDialog() == DialogResult.OK)
                            {
                                try
                                {
                                    profileImage = new BitmapImage(dialog.FileName, deviceContext, imagingFactory);

                                    profileBrush.Image = profileImage.Image;

                                    profileBrush.SourceRectangle = new RawRectangleF(0f, 0f, profileImage.Image.Size.Width, profileImage.Image.Size.Height);
                                    profileBrush.Transform = Matrix3x2.Scaling(1f, profileImage.Image.Size.Width / profileImage.Image.Size.Height);

                                    profileFullFileName = dialog.FileName;

                                    float profileBrushRadius = profileImage.Image.Size.Width / 2;
                                    profileImageGeometry = new EllipseGeometry(deviceContext.Factory, new Ellipse(new Vector2(profileBrushRadius, profileBrushRadius), profileBrushRadius, profileBrushRadius));

                                    showEmptyError.profile = false;
                                }
                                catch (Exception ex)
                                {
                                    MessageBox.Show(ex.Message);
                                }
                            }
                        }
                        else if (currentSelected.Equals(SelectableElement.Delete))
                        {
                            isShowingConfirmDelete = true;
                            currentSelected = SelectableElement.ConfirmDeleteNo;
                            playSelectedSound();
                        }
                    }
                    else
                    {
                        if (currentSelected.Equals(SelectableElement.ConfirmDeleteNo))
                        {
                            isShowingConfirmDelete = false;
                            currentSelected = SelectableElement.Delete;
                            playSelectedSound();
                        }
                        else if (currentSelected.Equals(SelectableElement.ConfirmDeleteYes))
                        {
                            DatabaseManager dbManager = new DatabaseManager();

                            if (dbManager.DeleteUser(editingUserID.Value))
                            {
                                changePanelCallback(new ProfileSelector(deviceContext, changePanelCallback));
                            }
                        }
                    }

                    break;
                case InputChoices.Cancel:
                    if (isShowingConfirmDelete)
                    {
                        playSelectedSound();
                        isShowingConfirmDelete = false;
                    }
                    else
                    {
                        playSelectedSound();
                        changePanelCallback(new ProfileSelector(deviceContext, changePanelCallback));
                    }
                    break;

                case InputChoices.Up:
                    if (!isShowingConfirmDelete)
                    {
                        switch (currentSelected)
                        {
                            case SelectableElement.Accept:
                                currentSelected = SelectableElement.Textbox;
                                break;

                            case SelectableElement.Cancel:
                                currentSelected = SelectableElement.Textbox;
                                break;

                            case SelectableElement.Textbox:
                                currentSelected = SelectableElement.Profile;
                                break;
                        }
                    }
                    break;

                case InputChoices.Down:
                    if (!isShowingConfirmDelete)
                    {
                        switch(currentSelected)
                        {
                            case SelectableElement.Profile:
                                currentSelected = SelectableElement.Textbox;
                                break;

                            case SelectableElement.Textbox:
                                currentSelected = SelectableElement.Accept;
                                break;
                        }
                    }
                    break;

                case InputChoices.Right:
                    if (!isShowingConfirmDelete)
                    {
                        switch(currentSelected)
                        {
                            case SelectableElement.Accept:
                                currentSelected = SelectableElement.Cancel;
                                break;

                            case SelectableElement.Cancel:
                            case SelectableElement.Textbox:
                            case SelectableElement.Profile:
                                if (isEditingUserFlag)
                                    currentSelected = SelectableElement.Delete;
                                break;
                        }
                    }
                    else
                    {
                        if(currentSelected.Equals(SelectableElement.ConfirmDeleteNo))
                        {
                            currentSelected = SelectableElement.ConfirmDeleteYes;
                        }
                    }
                    break;

                case InputChoices.Left:
                    if (!isShowingConfirmDelete)
                    {

                        switch (currentSelected)
                        {
                            case SelectableElement.Cancel:
                                currentSelected = SelectableElement.Accept;
                                break;

                            case SelectableElement.Delete:
                                currentSelected = SelectableElement.Profile;
                                break;
                        }
                    }
                    else
                    {
                        if (currentSelected.Equals(SelectableElement.ConfirmDeleteYes))
                        {
                            currentSelected = SelectableElement.ConfirmDeleteNo;
                        }
                    }
                    
                    break;
            }

            if (oldSelected != currentSelected)
                playSelectedSound();
        }

        private void playSelectedSound()
        {
            selectionClickVoice = new SourceVoice(audioOut, profileClickStream.Format, false);
            selectionClickVoice.SubmitSourceBuffer(selectionClickBuffer, profileClickStream.DecodedPacketsInfo);
            selectionClickVoice.SetVolume(0.5f);
            selectionClickVoice.Start();
        }

        private void reconnectControllers()
        {
            List<Controller> connectedControllers = new List<Controller>();
            List<State> connectedControllersState = new List<State>();

            //for each controller that is connected, save it into the list
            for (int i = 0; i < 4; i++)
            {
                Controller testController = new Controller((UserIndex)i - 1);


                if (testController.IsConnected)
                {
                    connectedControllers.Add(testController);
                    connectedControllersState.Add(testController.GetState());
                }
            }

            //use those lists to populate these arrays
            controllers = connectedControllers.ToArray();
            oldControllerState = connectedControllersState.ToArray();

            lastControllerReconnectTime = sw.ElapsedMilliseconds;

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

        public void Resize(DeviceContext deviceContext)
        {
            this.deviceContext = deviceContext;

            createSizeDependantResources(deviceContext);
        }

        public void Update()
        {
            //if we are not the currently active form then do nothing
            Form activeForm = System.Windows.Forms.Form.ActiveForm;
            if (activeForm == null)
                return;

            System.Drawing.Point currentMousePos = System.Windows.Forms.Cursor.Position;
            System.Drawing.Point mouseToScreen = activeForm.PointToClient(currentMousePos);
            Vector2 mousePos = new Vector2(mouseToScreen.X, mouseToScreen.Y);

            if (oldMousePos == null)
                oldMousePos = mousePos;

            if (mousePos != oldMousePos || mouseState.leftMouseReleased)
            {
                SelectableElement oldSelected = currentSelected;

                if (!isShowingConfirmDelete)
                {
                    if (Vector2.DistanceSquared(mousePos, new Vector2(deviceContext.Size.Width / 2, deviceContext.Size.Height / 4)) < profilePictureSize / 2 * currentScales.profilePicture * (profilePictureSize / 2 * currentScales.profilePicture))
                    {
                        currentSelected = SelectableElement.Profile;

                        if(mouseState.leftMouseReleased)
                            input(InputChoices.Enter, true);
                    }
                    else if (mousePos.X > usernameTextboxRect.Rect.Left && mousePos.X < usernameTextboxRect.Rect.Right && mousePos.Y > usernameTextboxRect.Rect.Top && mousePos.Y < usernameTextboxRect.Rect.Bottom)
                    {
                        currentSelected = SelectableElement.Textbox;

                        if (mouseState.leftMouseReleased)
                            input(InputChoices.Enter, true);
                    }
                    else if (Vector2.DistanceSquared(mousePos, new Vector2(acceptButtonGeometry.Ellipse.Point.X, acceptButtonGeometry.Ellipse.Point.Y)) < changePanelButtonSize / 2 * currentScales.acceptButton * (changePanelButtonSize / 2 * currentScales.acceptButton))
                    {
                        currentSelected = SelectableElement.Accept;

                        if (mouseState.leftMouseReleased)
                            input(InputChoices.Enter, true);
                    }
                    else if (Vector2.DistanceSquared(mousePos, new Vector2(cancelButtonGeometry.Ellipse.Point.X, cancelButtonGeometry.Ellipse.Point.Y)) < changePanelButtonSize / 2 * currentScales.cancelButton * (changePanelButtonSize / 2 * currentScales.cancelButton))
                    {
                        currentSelected = SelectableElement.Cancel;

                        if (mouseState.leftMouseReleased)
                            input(InputChoices.Enter, true);
                    }
                }

                if (isEditingUserFlag)
                {
                    if (isShowingConfirmDelete)
                    {
                        if (mousePos.X > confirmDeleteCancelButton.RoundedRect.Rect.Left && mousePos.X < confirmDeleteCancelButton.RoundedRect.Rect.Right && mousePos.Y > confirmDeleteCancelButton.RoundedRect.Rect.Top && mousePos.Y < confirmDeleteCancelButton.RoundedRect.Rect.Bottom)
                        {
                            currentSelected = SelectableElement.ConfirmDeleteNo;

                            if (mouseState.leftMouseReleased)
                                input(InputChoices.Enter, true);
                        }
                        else if (mousePos.X > confirmDeleteOKButton.RoundedRect.Rect.Left && mousePos.X < confirmDeleteOKButton.RoundedRect.Rect.Right && mousePos.Y > confirmDeleteOKButton.RoundedRect.Rect.Top && mousePos.Y < confirmDeleteOKButton.RoundedRect.Rect.Bottom)
                        {
                            currentSelected = SelectableElement.ConfirmDeleteYes;

                            if (mouseState.leftMouseReleased)
                                input(InputChoices.Enter, true);
                        }
                    }
                    else if (Vector2.DistanceSquared(mousePos, new Vector2(deleteGeometry.Ellipse.Point.X, deleteGeometry.Ellipse.Point.Y)) < deleteButtonSize / 2 * currentScales.deleteButton * (deleteButtonSize / 2 * currentScales.deleteButton))
                    {
                        currentSelected = SelectableElement.Delete;

                        if (mouseState.leftMouseReleased)
                            input(InputChoices.Enter, true);
                    }
                }

                if (currentSelected != oldSelected)
                    playSelectedSound();

                oldMousePos = mousePos;
                mouseState.leftMouseReleased = false;
            }

            //controller input

            //controller input
            if (lastControllerReconnectTime + 5000 < sw.ElapsedMilliseconds)
                reconnectControllers();

            for (int controller = 0; controller < controllers.Length; controller++)
            {
                //don't do anything if the controller isn't connected
                if (controllers[controller].IsConnected)
                {
                    //get the current state and the old state for comparison
                    State currentControllerState = controllers[controller].GetState();
                    State compareState = oldControllerState[controller];

                    Console.WriteLine((compareState.Gamepad.Buttons & (GamepadButtonFlags.A | GamepadButtonFlags.X)) == 0);

                    //if the left stick is past the dead zone this time and short the dead zone last time OR the d-pad is pressed
                    if (((float)currentControllerState.Gamepad.LeftThumbX / short.MaxValue > deadZone && ((float)compareState.Gamepad.LeftThumbX / short.MaxValue) < deadZone) ||
                            (currentControllerState.Gamepad.Buttons & GamepadButtonFlags.DPadRight) == GamepadButtonFlags.DPadRight && (compareState.Gamepad.Buttons & GamepadButtonFlags.DPadRight) == 0)
                    {
                        input(InputChoices.Right);
                    }
                    //same for this one except for left
                    else if (((float)currentControllerState.Gamepad.LeftThumbX / short.MaxValue * -1f > deadZone && (float)compareState.Gamepad.LeftThumbX / short.MaxValue * -1f < deadZone) ||
                        (currentControllerState.Gamepad.Buttons & GamepadButtonFlags.DPadLeft) == GamepadButtonFlags.DPadLeft && (compareState.Gamepad.Buttons & GamepadButtonFlags.DPadLeft) == 0)
                    {
                        input(InputChoices.Left);
                    }
                    //and for down
                    else if (((float)currentControllerState.Gamepad.LeftThumbY / short.MaxValue * -1f > deadZone && (float)compareState.Gamepad.LeftThumbY / short.MaxValue * -1f < deadZone) ||
                        (currentControllerState.Gamepad.Buttons & GamepadButtonFlags.DPadDown) == GamepadButtonFlags.DPadDown && (compareState.Gamepad.Buttons & GamepadButtonFlags.DPadDown) == 0)
                    {
                        input(InputChoices.Down);
                    }
                    //and for up
                    else if (((float)currentControllerState.Gamepad.LeftThumbY / short.MaxValue > deadZone && (float)compareState.Gamepad.LeftThumbY / short.MaxValue < deadZone) ||
                        (currentControllerState.Gamepad.Buttons & GamepadButtonFlags.DPadUp) == GamepadButtonFlags.DPadUp && (compareState.Gamepad.Buttons & GamepadButtonFlags.DPadUp) == 0)
                    {
                        input(InputChoices.Up);
                    }
                    //see if the 'A' or 'X' button was pressed this frame
                    else if (((currentControllerState.Gamepad.Buttons & GamepadButtonFlags.A) == GamepadButtonFlags.A || (currentControllerState.Gamepad.Buttons & GamepadButtonFlags.X) == GamepadButtonFlags.X) &&
                        (compareState.Gamepad.Buttons & (GamepadButtonFlags.X | GamepadButtonFlags.A)) == 0)
                    {
                        input(InputChoices.Enter);
                    }
                    //see if the 'B' button was pressed this frame
                    else if ((currentControllerState.Gamepad.Buttons & GamepadButtonFlags.B) == GamepadButtonFlags.B && (compareState.Gamepad.Buttons & GamepadButtonFlags.B) == 0)
                    {
                        input(InputChoices.Cancel);
                    }

                    oldControllerState[controller] = currentControllerState;
                }
            }

        }

        public void Draw(DeviceContext deviceContext)
        {
            //if we are not the currently active form then do nothing
            if (System.Windows.Forms.Form.ActiveForm == null)
                return;

            //start the stopwatch if it's not running
            if (!sw.IsRunning)
            {
                sw.Start();
                lastMilliseconds = sw.ElapsedMilliseconds;
            }

            // sets the lerp amount based on the last frametime
            float lerpAmount = (sw.ElapsedMilliseconds - lastMilliseconds) / 1000f * 20f;

            lastMilliseconds = sw.ElapsedMilliseconds;

            //handle scale amounts for each element
            currentScales.profilePicture = lerp(currentScales.profilePicture, currentSelected.Equals(SelectableElement.Profile) ? selectedScale * 0.9f : 1f, lerpAmount);
            currentScales.acceptButton = lerp(currentScales.acceptButton, currentSelected.Equals(SelectableElement.Accept) ? selectedScale : 1f, lerpAmount);
            currentScales.cancelButton = lerp(currentScales.cancelButton, currentSelected.Equals(SelectableElement.Cancel) ? selectedScale : 1f, lerpAmount);
            currentScales.deleteButton = lerp(currentScales.deleteButton, currentSelected.Equals(SelectableElement.Delete) ? selectedScale : 1f, lerpAmount);
            currentScales.usernameTextbox = lerp(currentScales.usernameTextbox, currentSelected.Equals(SelectableElement.Textbox) ? selectedScale * 0.9f : 1f, lerpAmount);
            currentScales.confirmDeleteYes = lerp(currentScales.confirmDeleteYes, currentSelected.Equals(SelectableElement.ConfirmDeleteYes) ? selectedScale : 1f, lerpAmount);
            currentScales.confirmDeleteNo = lerp(currentScales.confirmDeleteNo, currentSelected.Equals(SelectableElement.ConfirmDeleteNo) ? selectedScale : 1f, lerpAmount);


            deviceContext.Transform = Matrix3x2.Identity;

            deviceContext.BeginDraw();

            deviceContext.Clear(Color.Black);

            deviceContext.FillRectangle(new RawRectangleF(0f, 0f, deviceContext.Size.Width, deviceContext.Size.Height), backgroundGradientBrush);

            colorBrush.Color = Color.LightBlue; // new Color(150, 150, 150);     
            
            Matrix3x2 profileOffset = Matrix3x2.Translation(new Vector2(deviceContext.Size.Width / 2 - profileGeometry.Ellipse.RadiusX, deviceContext.Size.Height / 4 - profileGeometry.Ellipse.RadiusY));

            deviceContext.Transform = Matrix3x2.Multiply(Matrix3x2.Scaling(currentScales.profilePicture, currentScales.profilePicture, new Vector2(profileGeometry.Ellipse.RadiusX, profileGeometry.Ellipse.RadiusY)), profileOffset);
            deviceContext.FillEllipse(profileGeometry.Ellipse, colorBrush);

            if (profileImage != null)
            {
                deviceContext.Transform = Matrix3x2.Multiply(Matrix3x2.Scaling(profilePictureSize / profileImage.Image.Size.Width), deviceContext.Transform);

                deviceContext.FillEllipse(profileImageGeometry.Ellipse, profileBrush);
            }
            else
            {
                deviceContext.Transform = Matrix3x2.Multiply(Matrix3x2.Translation(new Vector2(profilePictureSize / 2 * 0.3f, profilePictureSize / 2 * 0.3f)), deviceContext.Transform);
                deviceContext.Transform = Matrix3x2.Multiply(Matrix3x2.Scaling(profilePictureSize / defaultProfileImage.Image.Size.Width * 0.7f), deviceContext.Transform);

                deviceContext.FillEllipse(defaultImageGeometry.Ellipse, profileBrush);
            }

            deviceContext.Transform = Matrix3x2.Multiply(Matrix3x2.Scaling(currentScales.profilePicture, currentScales.profilePicture, new Vector2(profileGeometry.Ellipse.RadiusX, profileGeometry.Ellipse.RadiusY)), profileOffset); ;
            deviceContext.StrokeWidth = borderThickness;


            if (currentSelected.Equals(SelectableElement.Profile))
            {
                colorBrush.Color = new Color(0, 0, 0, 50);
                deviceContext.FillGeometry(profileGeometry, colorBrush);

                colorBrush.Color = showEmptyError.profile ? new Color(255, 71, 68) : Color.LightGray;
                deviceContext.DrawEllipse(profileGeometry.Ellipse, colorBrush);

            }
            else
            {
                colorBrush.Color = showEmptyError.profile ? new Color(255, 71, 68) : Color.White;
                deviceContext.DrawEllipse(profileGeometry.Ellipse, colorBrush);
            }

            if (showEmptyError.profile)
            {
                deviceContext.Transform = Matrix3x2.Multiply(Matrix3x2.Scaling(errorIconSize / errorImage.Image.Size.Width), Matrix3x2.Multiply(profileOffset, Matrix3x2.Translation(new Vector2(((profilePictureSize / 2) - (errorProfileLayout.MaxWidth - errorIconSize) / 2) - errorIconSize, (profilePictureSize * (1f + ((currentScales.profilePicture - 1) / 2)))))));
                deviceContext.DrawImage(errorIconTinted);

                deviceContext.Transform = profileOffset;
                colorBrush.Color = new Color(255, 71, 68);
                deviceContext.DrawTextLayout(new Vector2((profilePictureSize / 2) - (errorProfileLayout.MaxWidth - errorIconSize - 20) / 2, (profilePictureSize * (1f + ((currentScales.profilePicture - 1) / 2))) + errorProfileLayout.FontSize / 2), errorProfileLayout, colorBrush);
            }

            //textbox
            colorBrush.Color = new Color(150, 150, 150, 125);
            if (currentSelected.Equals(SelectableElement.Textbox))
                colorBrush.Color = Color.AdjustContrast(new Color(colorBrush.Color.R, colorBrush.Color.G, colorBrush.Color.B, colorBrush.Color.A), 0.8f);

            //deviceContext.Transform = Matrix3x2.Multiply(Matrix3x2.Scaling(currentScales.usernameTextbox, currentScales.usernameTextbox, new Vector2(deviceContext.Size.Width / 2, deviceContext.Size.Height / 2)), Matrix3x2.Translation(new Vector2(10, 10)));
            //deviceContext.FillRoundedRectangle(usernameTextboxRect, colorBrush);

            deviceContext.Transform = Matrix3x2.Scaling(currentScales.usernameTextbox, currentScales.usernameTextbox, new Vector2(deviceContext.Size.Width / 2, deviceContext.Size.Height / 2));
            colorBrush.Color = Color.LightBlue;
            if (currentSelected.Equals(SelectableElement.Textbox))
                colorBrush.Color = Color.AdjustContrast(new Color(colorBrush.Color.R, colorBrush.Color.G, colorBrush.Color.B), 0.8f);

            deviceContext.FillRoundedRectangle(usernameTextboxRect, colorBrush);

            colorBrush.Color = showEmptyError.username ? new Color(255, 71, 68) : Color.White;
            if (currentSelected.Equals(SelectableElement.Textbox))
                colorBrush.Color = Color.AdjustContrast(new Color(colorBrush.Color.R, colorBrush.Color.G, colorBrush.Color.B), 0.8f);

            deviceContext.DrawRoundedRectangle(usernameTextboxRect, colorBrush);

            colorBrush.Color = (username == null || username == "") ? new Color(120, 157, 170) : Color.White;
            if (currentSelected.Equals(SelectableElement.Textbox))
                colorBrush.Color = Color.AdjustContrast(new Color(colorBrush.Color.R, colorBrush.Color.G, colorBrush.Color.B), 0.8f);

            TextLayout layout = (username == null || username == "") ? defaultUsernameLayout : usernameLayout;
            deviceContext.DrawTextLayout(new Vector2(usernameTextboxRect.Rect.Left + 50 * currentDeviceScale, usernameTextboxRect.Rect.Top - 10 * currentDeviceScale), layout, colorBrush);

            if (showEmptyError.username)
            {
                deviceContext.Transform = Matrix3x2.Multiply(Matrix3x2.Scaling(errorIconSize / errorImage.Image.Size.Width), Matrix3x2.Translation(new Vector2(deviceContext.Size.Width / 2 - ((errorUsernameLayout.MaxWidth - errorIconSize) / 2) - errorIconSize, usernameTextboxRect.Rect.Top + 10f + (usernameTextboxRect.Rect.Bottom - usernameTextboxRect.Rect.Top) * currentScales.usernameTextbox)));
                deviceContext.DrawImage(errorIconTinted);

                deviceContext.Transform = Matrix3x2.Translation(new Vector2(deviceContext.Size.Width / 2 - ((errorUsernameLayout.MaxWidth - errorIconSize - 20) / 2), usernameTextboxRect.Rect.Top + 10f + errorUsernameLayout.FontSize / 2 + (usernameTextboxRect.Rect.Bottom - usernameTextboxRect.Rect.Top) * currentScales.usernameTextbox));

                colorBrush.Color = new Color(255, 71, 68);
                deviceContext.DrawTextLayout(new Vector2(0, 0), errorUsernameLayout, colorBrush);
            }

            //accept and canel buttons
            deviceContext.StrokeWidth = borderThickness;
            colorBrush.Color = Color.LightGreen;
            deviceContext.Transform = Matrix3x2.Scaling(currentScales.acceptButton, currentScales.acceptButton, new Vector2(acceptButtonGeometry.Ellipse.Point.X, acceptButtonGeometry.Ellipse.Point.Y));
            deviceContext.FillGeometry(acceptButtonGeometry, colorBrush);

            deviceContext.DrawBitmap(acceptButtonImage.Image, new RawRectangleF(acceptButtonGeometry.Ellipse.Point.X - changePanelButtonSize / 2 * 0.7f, acceptButtonGeometry.Ellipse.Point.Y - changePanelButtonSize / 2 * 0.7f, acceptButtonGeometry.Ellipse.Point.X + changePanelButtonSize / 2 * 0.7f, acceptButtonGeometry.Ellipse.Point.Y + changePanelButtonSize / 2 * 0.7f), 1f, SharpDX.Direct2D1.BitmapInterpolationMode.Linear);

            colorBrush.Color = Color.White;
            deviceContext.DrawEllipse(acceptButtonGeometry.Ellipse, colorBrush);

            if (currentSelected.Equals(SelectableElement.Accept))
            {
                colorBrush.Color = new Color(0, 0, 0, 50);
                deviceContext.FillGeometry(acceptButtonGeometry, colorBrush);
                colorBrush.Color = Color.LightGray;
                deviceContext.DrawEllipse(acceptButtonGeometry.Ellipse, colorBrush);
            }
            deviceContext.Transform = Matrix3x2.Identity;

            deviceContext.Transform = Matrix3x2.Multiply(Matrix3x2.Scaling(currentScales.cancelButton, currentScales.cancelButton, new Vector2(cancelButtonGeometry.Ellipse.Point.X, cancelButtonGeometry.Ellipse.Point.Y)), deviceContext.Transform);

            colorBrush.Color = Color.Red;
            deviceContext.FillGeometry(cancelButtonGeometry, colorBrush);

            deviceContext.DrawBitmap(cancelButtonImage.Image, new RawRectangleF(cancelButtonGeometry.Ellipse.Point.X - changePanelButtonSize / 2 * 0.6f, cancelButtonGeometry.Ellipse.Point.Y - changePanelButtonSize / 2 * 0.6f, cancelButtonGeometry.Ellipse.Point.X + changePanelButtonSize / 2 * 0.6f, cancelButtonGeometry.Ellipse.Point.Y + changePanelButtonSize / 2 * 0.6f), 1f, SharpDX.Direct2D1.BitmapInterpolationMode.Linear);

            colorBrush.Color = Color.White;
            deviceContext.DrawEllipse(cancelButtonGeometry.Ellipse, colorBrush);

            if (currentSelected.Equals(SelectableElement.Cancel))
            {
                colorBrush.Color = new Color(0, 0, 0, 50);
                deviceContext.FillGeometry(cancelButtonGeometry, colorBrush);
                colorBrush.Color = Color.LightGray;
                deviceContext.DrawEllipse(cancelButtonGeometry.Ellipse, colorBrush);
            }
            deviceContext.Transform = Matrix3x2.Identity;


            if (isEditingUserFlag)
            {
                //delete button
                deviceContext.Transform = Matrix3x2.Multiply(Matrix3x2.Scaling(currentScales.deleteButton, currentScales.deleteButton, new Vector2(deleteGeometry.Ellipse.Point.X, deleteGeometry.Ellipse.Point.Y)), deviceContext.Transform);

                colorBrush.Color = new Color(200, 40, 15);
                deviceContext.FillGeometry(deleteGeometry, colorBrush);

                deviceContext.DrawBitmap(deleteImage.Image, new RawRectangleF(deleteGeometry.Ellipse.Point.X - deleteButtonSize / 3, deleteGeometry.Ellipse.Point.Y - deleteButtonSize / 3, deleteGeometry.Ellipse.Point.X + deleteButtonSize / 3, deleteGeometry.Ellipse.Point.Y + deleteButtonSize / 3), 1f, SharpDX.Direct2D1.BitmapInterpolationMode.Linear);

                colorBrush.Color = Color.White;
                deviceContext.StrokeWidth = borderThickness;
                deviceContext.DrawEllipse(deleteGeometry.Ellipse, colorBrush);

                if(currentSelected.Equals(SelectableElement.Delete) && !isShowingConfirmDelete)
                {
                    colorBrush.Color = new Color(0, 0, 0, 50);
                    deviceContext.FillGeometry(deleteGeometry, colorBrush);
                    colorBrush.Color = Color.LightGray;
                    deviceContext.DrawEllipse(deleteGeometry.Ellipse, colorBrush);
                }

                deviceContext.Transform = Matrix3x2.Identity;

                //confirm delete panel
                if (isShowingConfirmDelete)
                {
                    colorBrush.Color = new Color(0, 0, 0, 120);
                    deviceContext.FillRectangle(new RawRectangleF(0, 0, deviceContext.Size.Width, deviceContext.Size.Height), colorBrush);

                    colorBrush.Color = Color.White;
                    deviceContext.FillGeometry(confirmDeletePanel, colorBrush);

                    deviceContext.Transform = Matrix3x2.Scaling(currentScales.confirmDeleteNo, currentScales.confirmDeleteNo, new Vector2(confirmDeleteCancelButton.RoundedRect.Rect.Left +
                        (confirmDeleteCancelButton.RoundedRect.Rect.Right - confirmDeleteCancelButton.RoundedRect.Rect.Left) / 2, confirmDeleteCancelButton.RoundedRect.Rect.Top + 
                        (confirmDeleteCancelButton.RoundedRect.Rect.Bottom - confirmDeleteCancelButton.RoundedRect.Rect.Top) / 2));
                    colorBrush.Color = Color.LightGreen;
                    deviceContext.FillGeometry(confirmDeleteCancelButton, colorBrush);
                    colorBrush.Color = Color.White;
                    deviceContext.DrawTextLayout(new Vector2(confirmDeleteCancelButton.RoundedRect.Rect.Left, confirmDeleteCancelButton.RoundedRect.Rect.Top + ((confirmDeleteCancelButton.RoundedRect.Rect.Bottom - confirmDeleteCancelButton.RoundedRect.Rect.Top) / 2) - (confirmDeleteCancelText.MaxHeight / 2)), confirmDeleteCancelText, colorBrush);

                    colorBrush.Color = new Color(0, 0, 0, 35);
                    if (currentSelected.Equals(SelectableElement.ConfirmDeleteNo))
                        deviceContext.FillGeometry(confirmDeleteCancelButton, colorBrush);

                    deviceContext.Transform = Matrix3x2.Scaling(currentScales.confirmDeleteYes, currentScales.confirmDeleteYes, new Vector2(confirmDeleteOKButton.RoundedRect.Rect.Left +
                        (confirmDeleteOKButton.RoundedRect.Rect.Right - confirmDeleteOKButton.RoundedRect.Rect.Left) / 2, confirmDeleteOKButton.RoundedRect.Rect.Top +
                        (confirmDeleteOKButton.RoundedRect.Rect.Bottom - confirmDeleteOKButton.RoundedRect.Rect.Top) / 2));

                    colorBrush.Color = Color.Red;
                    deviceContext.FillGeometry(confirmDeleteOKButton, colorBrush);
                    colorBrush.Color = Color.White;
                    deviceContext.DrawTextLayout(new Vector2(confirmDeleteOKButton.RoundedRect.Rect.Left, confirmDeleteOKButton.RoundedRect.Rect.Top + ((confirmDeleteOKButton.RoundedRect.Rect.Bottom - confirmDeleteOKButton.RoundedRect.Rect.Top) / 2) - (confirmDeleteOKText.MaxHeight / 2)), confirmDeleteOKText, colorBrush);

                    colorBrush.Color = new Color(0, 0, 0, 35);
                    if (currentSelected.Equals(SelectableElement.ConfirmDeleteYes))
                        deviceContext.FillGeometry(confirmDeleteOKButton, colorBrush);

                    deviceContext.Transform = Matrix3x2.Translation(new Vector2(confirmDeletePanel.RoundedRect.Rect.Left + (confirmDeletePanel.RoundedRect.Rect.Right - confirmDeletePanel.RoundedRect.Rect.Left) / 2 - confirmDeletePanelImageSize / 2, confirmDeletePanel.RoundedRect.Rect.Top + 10f * currentDeviceScale));
                    deviceContext.FillRectangle(new RawRectangleF(0f, 0f, confirmDeletePanelImageSize, confirmDeletePanelImageSize), confirmDeletePanelImageBrush);
                    deviceContext.Transform = Matrix3x2.Identity;

                    colorBrush.Color = Color.Gray;
                    //deviceContext.DrawTextLayout(new RawVector2(confirmDeletePanel.RoundedRect.Rect.Left + 10f * currentDeviceScale, confirmDeletePanel.RoundedRect.Rect.Top + 20f * currentDeviceScale + confirmDeletePanelImageSize), confirmDeleteText, colorBrush);
                    deviceContext.DrawTextLayout(new RawVector2(confirmDeletePanel.RoundedRect.Rect.Left + 10f * currentDeviceScale, confirmDeletePanel.RoundedRect.Rect.Top + 40f * currentDeviceScale + confirmDeletePanelImageSize), confirmDeleteText, colorBrush);
                    
                }
            }

            deviceContext.EndDraw();
        }
    }
}
