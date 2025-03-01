using save_switcher.Elements;
using SharpDX;
using SharpDX.Direct2D1;
using SharpDX.DirectWrite;
using SharpDX.Mathematics.Interop;
using SharpDX.Multimedia;
using SharpDX.WIC;
using SharpDX.XAudio2;
using SharpDX.XInput;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;
using System.Windows.Input;

namespace save_switcher.Panels
{
    class ProfileSelector : IPanel, IMouseable
    {
        private readonly float baseProfilePictureSize = 300;
        private readonly float baseProfilePadding = 100;
        private readonly float baseUtilityButtonSize = 125;
        private readonly float baseUtilityButtonPadding = 30;
        private readonly float baseEditProfileSize = 100f;
        private readonly float baseBorderThickness = 5f;
        private readonly float baseAddUserPlusIconThickness = 20f;
        private readonly float baseEdgeMouseSelectionThickness = 30f;
        private readonly Size2 baseScalingResolution = new Size2(1920, 1080);

        private float utilityButtonSize;
        private float utilityButtonPadding;
        private readonly float utilityButtonSelectedScale = 1.2f;
        private Color utilityButtonColor = new Color(75, 95, 130);
        private (BitmapImage addUserImage, BitmapImage settingsImage) utilityButtonImages;
        private (float addUserScale, float settingsScale) utilityButtonScales;
        private (Vector2 addUserPosition, Vector2 settingsPosition) utilityButtonPositions;
        private int selectedUtilityIndex;
        private BitmapImage editProfileBitmap;
        private float editProfileSize;
        private bool isCursorOverEditButton = false;

        private BitmapImage inputImageXboxYButton;
        private BitmapImage inputImageSpacebar;
        private BitmapImage inputImageMouseRightClick;
        private TextLayout editProfileInputTextLayout;
        private InputType currentInputType = InputType.Mouse;

        private float profilePictureSize;
        private float profilePadding;
        private float edgeMouseSelectionThickness;
        private readonly float selectedScale = 1.3f;
        private int selectedUserIndex;

        private DeviceContext deviceContext;

        private InputLayers currentInputLayer = InputLayers.Users;

        private ProfileUser[] users;

        private TextLayout whosPlayingLayout;

        private SolidColorBrush colorBrush;
        private LinearGradientBrush backgroundGradientBrush;

        private float borderThickness;
        private StrokeStyle addUserStrokeStyle;
        private float addUserPlusIconThickness;
        private TextLayout addUserLayout;
        private float currentAddUserScale = 1f;

        private XAudio2 audioOut;
        private SoundStream profileClickStream;
        private AudioBuffer selectionClickBuffer;
        private SourceVoice selectionClickVoice;

        private DatabaseManager databaseManager;

        private Controller[] controllers;
        private readonly float deadZone = 0.8f;
        private State[] oldControllerState;
        private System.Drawing.Point oldMousePos;
        private bool mouseLeftButtonReleased;
        private bool mouseRightButtonReleased;
        private KeyState[] oldKeyState;
        private Key[] monitorKeys = new Key[]
        {
            Key.Left,
            Key.Right,
            Key.Up,
            Key.Down,
            Key.Enter,
            Key.Space,
            Key.Escape,
        };

        private Stopwatch sw;

        private float lastMilliseconds;
        private long lastMouseUserSelectedTime;
        private long lastControllerReconnectTime;
        private float lastStartX;
        private float targetStartX;

        private struct ProfileUser
        {
            public User User;
            public BitmapImage ProfileImage;
            public ImageBrush ProfileBrush;
            public TextLayout Layout;
            public float CurrentSelectedScale;
            public Geometry ProfileGeometry;
            public float ProfileImageSize { get { return ProfileImage.Image.Size.Width; } }
            public Vector2 ScreenPosition;
        }

        private enum InputLayers
        {
            Users,
            Utilities,
            EditProfile,
        }

        private struct KeyState
        {
            public Key key;
            public bool isPressed;
        }
        
        private enum Inputs
        {
            None,
            Left,
            Right,
            Up,
            Down,
            Enter,
            AltEnter,
            Back,
        }

        private enum InputType
        {
            Mouse,
            Keyboard,
            Controller,
        }

        public ProfileSelector(DeviceContext deviceContext) {

            this.deviceContext = deviceContext;

            sw = new Stopwatch();

            //set up inputs
            reconnectControllers();

            //populate oldKeyState
            oldKeyState = new KeyState[monitorKeys.Length];
            for(int i = 0; i < monitorKeys.Length; i++)
            {
                oldKeyState[i] = new KeyState
                {
                    key = monitorKeys[i],
                    isPressed = true
                };
            }

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


            ImagingFactory imagingFactory = new ImagingFactory();
            databaseManager = new DatabaseManager();

            //create users
            User[] dbUsers = Program.GameID == null ? databaseManager.GetAllUsers() : databaseManager.GetAllUsers(Program.GameID.Value);
            users = dbUsers != null ? new ProfileUser[dbUsers.Length] : new ProfileUser[0];

            for(int i = 0; i < users.Length; i++)
            {
                string profilePicture = "";

                string userProfilePicture = $@"syncs\user_data\{dbUsers[i].ID}\profile.png";
                if (!File.Exists(userProfilePicture))
                    profilePicture = "Media/default_user_profile.png";
                else
                    profilePicture = userProfilePicture;
                
                BitmapImage image = new BitmapImage(Path.GetFullPath(profilePicture), deviceContext, imagingFactory);

                float imageRadius = image.Image.Size.Width / 2;
                //users[i] = new ProfileUser(image, dbUsers[i], layout, new EllipseGeometry(deviceContext.Factory, new Ellipse(new RawVector2(imageRadius, imageRadius), imageRadius, imageRadius)), deviceContext);

                users[i] = new ProfileUser
                {
                    User = dbUsers[i],
                    ProfileImage = image,
                    ProfileBrush = new ImageBrush(deviceContext, image.Image, new ImageBrushProperties { ExtendModeX = ExtendMode.Clamp, ExtendModeY = ExtendMode.Clamp, InterpolationMode = SharpDX.Direct2D1.InterpolationMode.Linear, SourceRectangle = new RawRectangleF(0f, 0f, image.Image.Size.Width, image.Image.Size.Height) }),
                    CurrentSelectedScale = 1f,
                    ProfileGeometry = new EllipseGeometry(deviceContext.Factory, new Ellipse(new RawVector2(imageRadius, imageRadius), imageRadius, imageRadius)),
                    ScreenPosition = Vector2.Zero,
                    //NOTE: Layout is set when we call createSizeDependantResources()
                };
                users[i].ProfileBrush.Transform = Matrix3x2.Scaling(1f, users[i].ProfileImage.Image.Size.Width / users[i].ProfileImage.Image.Size.Height);
            }

            //set up utility buttons
            utilityButtonScales.addUserScale = 1f;
            utilityButtonScales.settingsScale = 1f;

            utilityButtonImages.addUserImage = new BitmapImage("Media/add_user.png", deviceContext, imagingFactory);
            utilityButtonImages.settingsImage = new BitmapImage("Media/settings.png", deviceContext, imagingFactory);

            editProfileBitmap = new BitmapImage("Media/edit.png", deviceContext, imagingFactory);

            StrokeStyleProperties strokeProperties = new StrokeStyleProperties()
            {
                StartCap = CapStyle.Round,
                EndCap = CapStyle.Round,
                LineJoin = LineJoin.Round,
                DashStyle = DashStyle.Solid,
            };

            addUserStrokeStyle = new StrokeStyle(deviceContext.Factory, strokeProperties);

            //set up input images
            inputImageMouseRightClick = new BitmapImage("Media/mouse_right_click.png", deviceContext, imagingFactory);
            inputImageSpacebar = new BitmapImage("Media/spacebar.png", deviceContext, imagingFactory);
            inputImageXboxYButton = new BitmapImage("Media/xbox_y_button.png", deviceContext, imagingFactory);

            createSizeDependantResources(deviceContext);
            targetStartX = profilePadding;

            //start the stopwatch
            sw.Start();
            
        }

        public void OnMouseDown(System.Windows.Forms.MouseEventArgs e) { }

        public void OnMouseUp(System.Windows.Forms.MouseEventArgs e)
        {
            //for use in Update()
            if (e.Button == System.Windows.Forms.MouseButtons.Left)
                mouseLeftButtonReleased = true;
            else if (e.Button == System.Windows.Forms.MouseButtons.Right)
                mouseRightButtonReleased = true;
        }

        public void OnMouseWheel(System.Windows.Forms.MouseEventArgs e)
        {
            if (e.Delta > 0)
                selectedUserIndex = clamp(selectedUserIndex + 1, 0, users.Length - 1);
            else if (e.Delta < 0)
                selectedUserIndex = clamp(selectedUserIndex - 1, 0, users.Length - 1);
        }

        private void createSizeDependantResources(DeviceContext deviceContext)
        {
            float sizeScaling = deviceContext.Size.Height / baseScalingResolution.Height * (deviceContext.Size.Height / baseScalingResolution.Height / (deviceContext.Size.Width / baseScalingResolution.Width));

            profilePictureSize = baseProfilePictureSize * sizeScaling;
            profilePadding = baseProfilePadding * sizeScaling;
            utilityButtonSize = baseUtilityButtonSize * sizeScaling;
            utilityButtonPadding = baseUtilityButtonPadding * sizeScaling;
            editProfileSize = baseEditProfileSize * sizeScaling;
            borderThickness = baseBorderThickness * sizeScaling;
            edgeMouseSelectionThickness = baseEdgeMouseSelectionThickness * sizeScaling;
            addUserPlusIconThickness = baseAddUserPlusIconThickness * sizeScaling;

            //set up brushes
            colorBrush = new SolidColorBrush(deviceContext, Color.White);

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


            //set up text formats
            SharpDX.DirectWrite.Factory directWriteFactory = new SharpDX.DirectWrite.Factory();
            CustomFontCollectionLoader customFontLoader = new CustomFontCollectionLoader(directWriteFactory);
            SharpDX.DirectWrite.FontCollection fontCollection = new SharpDX.DirectWrite.FontCollection(directWriteFactory, customFontLoader, customFontLoader.KeyStream);

            TextFormat usernameFormat = new TextFormat(directWriteFactory, "Gabarito", fontCollection, FontWeight.Normal, FontStyle.Normal, FontStretch.Normal, 30f * sizeScaling);


            for (int i = 0; i < users.Length; i++)
            {
                users[i].Layout = new TextLayout(directWriteFactory, users[i].User.Username, usernameFormat, profilePictureSize, 20f * sizeScaling);
                users[i].Layout.ParagraphAlignment = ParagraphAlignment.Center;
                users[i].Layout.TextAlignment = TextAlignment.Center;
                users[i].Layout.WordWrapping = WordWrapping.Wrap;
            }

            addUserLayout = new TextLayout(directWriteFactory, "Add User", usernameFormat, profilePictureSize, 20f * sizeScaling);
            addUserLayout.ParagraphAlignment = ParagraphAlignment.Center;
            addUserLayout.TextAlignment = TextAlignment.Center;
            addUserLayout.WordWrapping = WordWrapping.Wrap;

            editProfileInputTextLayout = new TextLayout(directWriteFactory, "Edit Profile", usernameFormat, 200f * sizeScaling, 30f * sizeScaling);

            TextFormat textFormat = new TextFormat(directWriteFactory, "Gabarito", fontCollection, FontWeight.Normal, FontStyle.Normal, FontStretch.Normal, 100f * sizeScaling);
            whosPlayingLayout = new TextLayout(directWriteFactory, "Who's Playing?", textFormat, 800f * sizeScaling, 200f * sizeScaling);
            whosPlayingLayout.ParagraphAlignment = ParagraphAlignment.Center;
            whosPlayingLayout.TextAlignment = TextAlignment.Center;
            whosPlayingLayout.WordWrapping = WordWrapping.NoWrap;

            directWriteFactory.Dispose();
            fontCollection.Dispose();
            customFontLoader.Dispose();
            usernameFormat.Dispose();

            //resize utility buttons
            utilityButtonPositions.settingsPosition = new Vector2(deviceContext.Size.Width - (utilityButtonPadding + utilityButtonSize / 2), deviceContext.Size.Height - (utilityButtonSize / 2) - utilityButtonPadding);
            utilityButtonPositions.addUserPosition = new Vector2(deviceContext.Size.Width - (utilityButtonPadding * 2 + utilityButtonSize * 1.5f), deviceContext.Size.Height - (utilityButtonSize / 2) - utilityButtonPadding);

        }

        private void changePanel()
        {
            //this method is called from Update() when the 'enter' event is triggered
            //it's used when we are done with this panel and want to switch to a new one
            
            //switch the panel based on what the current input selection is
            if(currentInputLayer == InputLayers.Users)
            {
                Dispose();
                //do the save swapping and start the game
                if (users.Length > 0)
                    Program.ChangePanel(new RunGame(Program.GameID, users[selectedUserIndex].User.ID, deviceContext));
                else
                    Program.ChangePanel(new AddUser(deviceContext));
            }
            else if (currentInputLayer == InputLayers.Utilities)
            {
                if (selectedUtilityIndex == 0)
                {
                    Dispose();
                    //switch to the add user panel
                    Program.ChangePanel(new AddUser(deviceContext));
                }
                else if (selectedUtilityIndex == 1)
                    Program.ChangePanel(new Settings(deviceContext));
            }
            else if(currentInputLayer == InputLayers.EditProfile)
            {
                //switch to the add user panel to edit a user
                if(users.Length > 0)
                    Program.ChangePanel(new AddUser(deviceContext, users[selectedUserIndex].User));
            }
        } 

        public void Resize(DeviceContext deviceContext)
        {
            this.deviceContext = deviceContext;

            colorBrush.Dispose();
            backgroundGradientBrush.Dispose();

            for(int i = 0; i < users.Length; i++)
            {
                users[i].Layout.Dispose();
            }

            addUserLayout.Dispose();
            editProfileInputTextLayout.Dispose();

            whosPlayingLayout.Dispose();

            createSizeDependantResources(deviceContext);
        }

        public void Update()
        {
            //if we are not the currently active form then do nothing
            Form activeForm = System.Windows.Forms.Form.ActiveForm;
            if (activeForm == null)
                return;

            //this is updated throughout the method and used at the end to determine what action to take
            Inputs inputs = Inputs.None;

            //this is also used at the end to compare if anything changed
            (int user, int utility, InputLayers layer) oldIndex;
            oldIndex.user = selectedUserIndex;
            oldIndex.utility = selectedUtilityIndex;
            oldIndex.layer = currentInputLayer;

            //mouse input
            System.Drawing.Point currentMousePos = System.Windows.Forms.Cursor.Position;

            //if something happened with the mouse then continue
            if (currentMousePos != oldMousePos || mouseLeftButtonReleased || mouseRightButtonReleased || ((lastMouseUserSelectedTime + 200 < sw.ElapsedMilliseconds) && currentInputType == InputType.Mouse))
            {
                //gets the current mouse position and converts it to a vector2
                System.Drawing.Point mouseToScreen = activeForm.PointToClient(currentMousePos);
                Vector2 mousePos = new Vector2(mouseToScreen.X, mouseToScreen.Y);

                currentInputType = InputType.Mouse;

                //using distance squared throughout this for better performance
                //check for cursor over the 'add user' button
                if (Vector2.DistanceSquared(utilityButtonPositions.addUserPosition, mousePos) < (utilityButtonSize / 2 * utilityButtonScales.addUserScale) * (utilityButtonSize / 2 * utilityButtonScales.addUserScale))
                {
                    //set the current input layer
                    currentInputLayer = InputLayers.Utilities;

                    //set the current index
                    selectedUtilityIndex = 0;

                    //set our input selection
                    if (mouseLeftButtonReleased)
                        inputs = Inputs.Enter;
                    
                }
                //check for cursor over the 'settings' button
                else if (Vector2.DistanceSquared(utilityButtonPositions.settingsPosition, mousePos) < (utilityButtonSize / 2 * utilityButtonScales.settingsScale) * (utilityButtonSize / 2 * utilityButtonScales.settingsScale))
                {
                    //set the current input layer
                    currentInputLayer = InputLayers.Utilities;

                    //set the current index
                    selectedUtilityIndex = 1;

                    //set our input selection
                    if (mouseLeftButtonReleased)
                        inputs = Inputs.Enter;
                }
                else if(users.Length == 0 && Vector2.DistanceSquared(new Vector2(deviceContext.Size.Width / 2, deviceContext.Size.Height / 2), mousePos) < profilePictureSize * currentAddUserScale / 2 * (profilePictureSize * currentAddUserScale / 2))
                {
                    currentInputLayer = InputLayers.Users;
                    selectedUserIndex = 0;

                    if(mouseLeftButtonReleased)
                        inputs = Inputs.Enter;
                }
                //check for cursor over the user profile buttons
                else
                {
                    for (int i = 0; i < users.Length; i++)
                    {
                        //get the center of the profile in screen coordinates
                        Vector2 profileCenter = new Vector2(users[i].ScreenPosition.X + (profilePictureSize * users[i].CurrentSelectedScale / 2), users[i].ScreenPosition.Y + (profilePictureSize * users[i].CurrentSelectedScale / 2));

                        if (Vector2.DistanceSquared(profileCenter, mousePos) < (profilePictureSize / 2 * users[i].CurrentSelectedScale) * (profilePictureSize / 2 * users[i].CurrentSelectedScale))
                        {
                            if (currentMousePos != oldMousePos)
                            {
                                //set the current profile index
                                selectedUserIndex = i;

                                lastMouseUserSelectedTime = sw.ElapsedMilliseconds;
                            }

                            if (currentInputLayer != InputLayers.Users && currentInputLayer != InputLayers.EditProfile)
                                //sets the current input layer
                                currentInputLayer = InputLayers.Users;

                            if (currentInputLayer == InputLayers.EditProfile)
                            {
                                isCursorOverEditButton = Vector2.DistanceSquared(profileCenter, mousePos) < (editProfileSize * users[i].CurrentSelectedScale / 2) * (editProfileSize * users[i].CurrentSelectedScale / 2);

                                if (oldIndex.user != selectedUserIndex)
                                    currentInputLayer = InputLayers.Users;
                            }

                            //set our input selection
                            if (mouseLeftButtonReleased)
                                inputs = Inputs.Enter;
                            else if (mouseRightButtonReleased)
                                inputs = Inputs.AltEnter;
                        }
                    }
                }

                if (currentInputType == InputType.Mouse && lastMouseUserSelectedTime + 200 < sw.ElapsedMilliseconds && mousePos.Y < deviceContext.Size.Height / 2 + profilePictureSize / 2 && mousePos.Y > deviceContext.Size.Height / 2 - profilePictureSize / 2) {
                    if (mousePos.X < edgeMouseSelectionThickness && mousePos.X > -1)
                    {
                        //left side of screen
                        selectedUserIndex = clamp(selectedUserIndex - 1, 0, users.Length - 1);
                        lastMouseUserSelectedTime = sw.ElapsedMilliseconds;
                    }
                    else if(mousePos.X > deviceContext.Size.Width - edgeMouseSelectionThickness && mousePos.X < deviceContext.Size.Width + 1)
                    {
                        selectedUserIndex = clamp(selectedUserIndex + 1, 0, users.Length - 1);
                        lastMouseUserSelectedTime = sw.ElapsedMilliseconds;
                    }
                }

            }

            //keyboard input
            //get the current keyboard state
            KeyState[] currentKeyState = new KeyState[monitorKeys.Length];

            for (int i = 0; i < monitorKeys.Length; i++)
            {
                //set the currentKeyState based on what keys we want to monitor
                //  (defined in the header of this class)
                bool isPressed = Keyboard.IsKeyDown(monitorKeys[i]);
                currentKeyState[i] = new KeyState
                {
                    key = monitorKeys[i],
                    isPressed = isPressed,
                };
            }

            //do things based on what was pressed
            for (int i = 0; i < monitorKeys.Length; i++)
            {
                if (currentKeyState[i].isPressed && !oldKeyState[i].isPressed)
                {
                    currentInputType = InputType.Keyboard;

                    switch (currentKeyState[i].key)
                    {
                        case Key.Left:
                            inputs = Inputs.Left;
                            break;
                        case Key.Right:
                            inputs = Inputs.Right;
                            break;
                        case Key.Up:
                            inputs = Inputs.Up;
                            break;
                        case Key.Down:
                            inputs = Inputs.Down;
                            break;
                        case Key.Enter:
                            inputs = Inputs.Enter;
                            break;
                        case Key.Space:
                            inputs = Inputs.AltEnter;
                            break;
                        case Key.Escape:
                            inputs = Inputs.Back;
                            break;

                    }

                }
            }

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

                    //if the left stick is past the dead zone this time and short the dead zone last time OR the d-pad is pressed
                    if (((float)currentControllerState.Gamepad.LeftThumbX / short.MaxValue > deadZone && ((float)compareState.Gamepad.LeftThumbX / short.MaxValue) < deadZone) ||
                            (currentControllerState.Gamepad.Buttons & GamepadButtonFlags.DPadRight) == GamepadButtonFlags.DPadRight && (compareState.Gamepad.Buttons & GamepadButtonFlags.DPadRight) == 0)
                    {
                        inputs = Inputs.Right;
                        currentInputType = InputType.Controller;
                    }
                    //same for this one except for left
                    else if (((float)currentControllerState.Gamepad.LeftThumbX / short.MaxValue * -1f > deadZone && (float)compareState.Gamepad.LeftThumbX / short.MaxValue * -1f < deadZone) ||
                        (currentControllerState.Gamepad.Buttons & GamepadButtonFlags.DPadLeft) == GamepadButtonFlags.DPadLeft && (compareState.Gamepad.Buttons & GamepadButtonFlags.DPadLeft) == 0)
                    {
                        inputs = Inputs.Left;
                        currentInputType = InputType.Controller;
                    }
                    //and for down
                    else if (((float)currentControllerState.Gamepad.LeftThumbY / short.MaxValue * -1f > deadZone && (float)compareState.Gamepad.LeftThumbY / short.MaxValue * -1f < deadZone) ||
                        (currentControllerState.Gamepad.Buttons & GamepadButtonFlags.DPadDown) == GamepadButtonFlags.DPadDown && (compareState.Gamepad.Buttons & GamepadButtonFlags.DPadDown) == 0)
                    {
                        inputs = Inputs.Down;
                        currentInputType = InputType.Controller;
                    }
                    //and for up
                    else if (((float)currentControllerState.Gamepad.LeftThumbY / short.MaxValue > deadZone && (float)compareState.Gamepad.LeftThumbY / short.MaxValue < deadZone) ||
                        (currentControllerState.Gamepad.Buttons & GamepadButtonFlags.DPadUp) == GamepadButtonFlags.DPadUp && (compareState.Gamepad.Buttons & GamepadButtonFlags.DPadUp) == 0)
                    {
                        inputs = Inputs.Up;
                        currentInputType = InputType.Controller;
                    }
                    //see if the 'A' or 'X' button was pressed this frame
                    else if (((currentControllerState.Gamepad.Buttons & GamepadButtonFlags.A) == GamepadButtonFlags.A || (currentControllerState.Gamepad.Buttons & GamepadButtonFlags.X) == GamepadButtonFlags.X) &&
                        (compareState.Gamepad.Buttons & (GamepadButtonFlags.X | GamepadButtonFlags.A)) == 0)
                    {
                        inputs = Inputs.Enter;
                        currentInputType = InputType.Controller;
                    }
                    //see if the 'Y' button was pressed this frame
                    else if((currentControllerState.Gamepad.Buttons & GamepadButtonFlags.Y) == GamepadButtonFlags.Y && (compareState.Gamepad.Buttons & GamepadButtonFlags.Y) == 0)
                    {
                        inputs = Inputs.AltEnter;
                        currentInputType = InputType.Controller;
                    }
                    //see if the 'B' button was pressed this frame
                    else if((currentControllerState.Gamepad.Buttons & GamepadButtonFlags.B) == GamepadButtonFlags.B && (compareState.Gamepad.Buttons & GamepadButtonFlags.B) == 0)
                    {
                        inputs = Inputs.Back;
                        currentInputType = InputType.Controller;
                    }

                    oldControllerState[controller] = currentControllerState;
                }
            }

            //do things based on what inputs we got earlier
            switch (inputs)
            {
                case Inputs.Left:
                    if (currentInputLayer == InputLayers.EditProfile)
                        currentInputLayer = InputLayers.Users;

                    //change the index of different layers based on what layer we are currently on
                    if (users.Length > 0 && selectedUserIndex >= 0 && currentInputLayer == InputLayers.Users)
                    {
                        selectedUserIndex = clamp(selectedUserIndex - 1, 0, users.Length - 1);
                    }
                    else if(currentInputLayer == InputLayers.Utilities)
                    {
                        selectedUtilityIndex = clamp(selectedUtilityIndex - 1, 0, 1);
                    }

                    break;

                case Inputs.Right:
                    if (currentInputLayer == InputLayers.EditProfile)
                        currentInputLayer = InputLayers.Users;


                    //change the index of different layers based on what layer we are currently on
                    if (users.Length > 0 && selectedUserIndex >= 0 && currentInputLayer == InputLayers.Users)
                    {
                        selectedUserIndex = clamp(selectedUserIndex + 1, 0, users.Length - 1);
                    }
                    else if (currentInputLayer == InputLayers.Utilities)
                    {
                        selectedUtilityIndex = clamp(selectedUtilityIndex + 1, 0, 1);
                    }
                    break;

                case Inputs.Up:
                    //change the current input layer
                    if (currentInputLayer == InputLayers.Utilities)
                        currentInputLayer = InputLayers.Users;
                    break;

                case Inputs.Down:
                    //change the current input layer
                    if (currentInputLayer == InputLayers.Users || currentInputLayer == InputLayers.EditProfile)
                    {
                        currentInputLayer = InputLayers.Utilities;
                        selectedUtilityIndex = 0;
                    }
                    break;

                case Inputs.Enter:
                    changePanel();
                    break;

                case Inputs.AltEnter:
                    if (currentInputLayer == InputLayers.Users && users.Length > 0)
                        currentInputLayer = InputLayers.EditProfile;
                    else if (currentInputLayer == InputLayers.EditProfile)
                        currentInputLayer = InputLayers.Users;
                    break;

                case Inputs.Back:
                    switch (currentInputLayer)
                    {
                        case InputLayers.Utilities:
                                currentInputLayer = InputLayers.Users;
                            break;
                        case InputLayers.EditProfile:
                                currentInputLayer = InputLayers.Users;
                            break;
                        case InputLayers.Users:
                            Application.Exit();
                            break;
                    }
                    
                    break;
            }

            //play a sound if a section changed
            if (selectedUserIndex != oldIndex.user || selectedUtilityIndex != oldIndex.utility || currentInputLayer != oldIndex.layer)
            {
                selectionClickVoice = new SourceVoice(audioOut, profileClickStream.Format, false);
                selectionClickVoice.SubmitSourceBuffer(selectionClickBuffer, profileClickStream.DecodedPacketsInfo);
                selectionClickVoice.SetVolume(0.5f);
                selectionClickVoice.Start();
            }

            //set old states for comparison next frame
            oldMousePos = currentMousePos;
            oldKeyState = currentKeyState;
            mouseLeftButtonReleased = false;
            mouseRightButtonReleased = false;
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

            //start drawing
            deviceContext.BeginDraw();

            deviceContext.Clear(Color.Black);

            //draw background
            deviceContext.FillRectangle(new RawRectangleF(0, 0, deviceContext.Size.Width, deviceContext.Size.Height), backgroundGradientBrush);

            //get the center of the screen
            RawVector2 center = new RawVector2(deviceContext.Size.Width / 2, deviceContext.Size.Height / 2);

            //draw a 'add user' button in the users location if there are none
            if (users.Length == 0)
            {
                currentAddUserScale = lerp(currentAddUserScale, (currentInputLayer == InputLayers.Users) ? selectedScale : 1f, lerpAmount);

                deviceContext.Transform = Matrix3x2.Multiply(Matrix3x2.Scaling(currentAddUserScale, currentAddUserScale, new Vector2(deviceContext.Size.Width / 2, deviceContext.Size.Height / 2)), deviceContext.Transform);
                colorBrush.Color = new Color(0.25f, 0.25f, 0.25f, 0.5f);
                deviceContext.FillEllipse(new Ellipse(new Vector2(deviceContext.Size.Width / 2, deviceContext.Size.Height / 2), profilePictureSize / 2, profilePictureSize / 2), colorBrush);
                colorBrush.Color = Color.White;
                deviceContext.DrawLine(new Vector2(deviceContext.Size.Width / 2 - profilePictureSize / 5, deviceContext.Size.Height / 2), new Vector2(deviceContext.Size.Width / 2 + profilePictureSize / 5, deviceContext.Size.Height / 2), colorBrush, addUserPlusIconThickness, addUserStrokeStyle);
                deviceContext.DrawLine(new Vector2(deviceContext.Size.Width / 2, deviceContext.Size.Height / 2 - profilePictureSize / 5), new Vector2(deviceContext.Size.Width / 2, deviceContext.Size.Height / 2 + profilePictureSize / 5), colorBrush, addUserPlusIconThickness, addUserStrokeStyle);

                deviceContext.Transform = Matrix3x2.Identity;

                deviceContext.DrawTextLayout(new Vector2(deviceContext.Size.Width / 2 - profilePictureSize / 2, deviceContext.Size.Height / 2 + (profilePictureSize / 2 * currentAddUserScale + 30f)), addUserLayout, colorBrush);
            }


            if ((profilePictureSize + profilePadding) * users.Length > deviceContext.Size.Width)
            {
                if (lastStartX + ((selectedUserIndex) * (profilePictureSize + profilePadding)) < 0)
                {
                    targetStartX = 0 - (profilePictureSize + profilePadding) * selectedUserIndex + profilePadding;
                }
                else if ((selectedUserIndex + 1) * (profilePictureSize + profilePadding) + lastStartX > deviceContext.Size.Width)
                {
                    targetStartX = deviceContext.Size.Width - (clamp(selectedUserIndex + 1, 0, users.Length) * (profilePictureSize + profilePadding));
                }
            }
            else
            {
                targetStartX = center.X - (users.Length * (profilePictureSize + profilePadding) - profilePadding) / 2;
            }

            //lerp from the last start position to what it is now for smooth scrolling
            float startPositionX = lerp(lastStartX, targetStartX, lerpAmount);

            //used for the smooth scrolling above
            lastStartX = startPositionX;
            lastMilliseconds = sw.ElapsedMilliseconds;

            //iterate over all user profiles
            for (int user = 0; user < users.Length; user++)
            {
                //lerp from the current scale to target scale for smooth animations
                float currentScale;

                if (user == selectedUserIndex && (currentInputLayer == InputLayers.Users || currentInputLayer == InputLayers.EditProfile))
                    //lerp to selected scale
                    currentScale = lerp(users[user].CurrentSelectedScale, selectedScale, lerpAmount);
                else
                    //lerp to unselected scale
                    currentScale = lerp(users[user].CurrentSelectedScale, 1f, lerpAmount);

                users[user].CurrentSelectedScale = currentScale;

                //move based on what user is selected, and what it's current scale is
                deviceContext.Transform = Matrix3x2.Translation(startPositionX + ((profilePadding + profilePictureSize) * user) + (profilePictureSize - (profilePictureSize * users[user].CurrentSelectedScale)) / 2, (profilePictureSize / users[user].ProfileImageSize) + center.Y - (profilePictureSize * users[user].CurrentSelectedScale / 2));

                //because we are drawing a profile bitmap with geometry we have to scale the deviceContext transform based on how big the bitmap is
                float scalingFactor = profilePictureSize / users[user].ProfileImageSize * users[user].CurrentSelectedScale;
                deviceContext.Transform = Matrix3x2.Multiply(Matrix3x2.Scaling(scalingFactor, scalingFactor, new Vector2(profilePictureSize / users[user].ProfileImageSize / 2, profilePictureSize / users[user].ProfileImageSize / 2)), deviceContext.Transform);

                //draw the profile picture
                colorBrush.Color = Color.Black;
                deviceContext.FillGeometry(users[user].ProfileGeometry, colorBrush);
                deviceContext.FillGeometry(users[user].ProfileGeometry, users[user].ProfileBrush);

                //draw the edit profile graphics
                if (user == selectedUserIndex && currentInputLayer == InputLayers.EditProfile)
                {
                    colorBrush.Color = new Color(255, 255, 255, 150); //white with alpha
                    deviceContext.FillGeometry(users[user].ProfileGeometry, colorBrush);

                    colorBrush.Color = utilityButtonColor;
                    float inverseScalingFactor = users[user].ProfileImageSize / profilePictureSize;
                    Ellipse editButtonEllipse = new Ellipse(new RawVector2(users[user].ProfileImageSize / 2, users[user].ProfileImageSize / 2), editProfileSize / 2 * inverseScalingFactor, editProfileSize / 2 * inverseScalingFactor);

                    deviceContext.FillEllipse(editButtonEllipse, colorBrush);
                    deviceContext.DrawBitmap(editProfileBitmap.Image, new RawRectangleF(users[user].ProfileImageSize / 2 - (editProfileSize / 4 * inverseScalingFactor), users[user].ProfileImageSize / 2 - (editProfileSize / 4 * inverseScalingFactor), users[user].ProfileImageSize / 2 + (editProfileSize / 4 * inverseScalingFactor), users[user].ProfileImageSize / 2 + (editProfileSize / 4 * inverseScalingFactor)), 1f, SharpDX.Direct2D1.BitmapInterpolationMode.Linear);

                    if (isCursorOverEditButton)
                    {
                        colorBrush.Color = new Color(0, 0, 0, 100);
                        deviceContext.FillEllipse(editButtonEllipse, colorBrush);
                    }
                }

                //draw the outline - the stroke width is changed to the inverse of our scaling factor to keep a uniform thickness regardless of what our transform is
                //  (we can't use an identity matrix because we have to keep the same geometry as the profile picture)
                colorBrush.Color = Color.White;
                deviceContext.StrokeWidth = borderThickness * (users[user].ProfileImageSize / profilePictureSize);
                deviceContext.DrawGeometry(users[user].ProfileGeometry, colorBrush);

                //updates the user's screen position in global screen postion for use later in Update()
                users[user].ScreenPosition = new Vector2(deviceContext.Transform.M31, deviceContext.Transform.M32);

                //reset the matrix to default
                deviceContext.Transform = Matrix3x2.Identity;

                //draw the user's username
                deviceContext.DrawTextLayout(new RawVector2(startPositionX + (profilePadding + profilePictureSize) * user, center.Y + (profilePictureSize * users[user].CurrentSelectedScale / 2) + users[user].Layout.FontSize), users[user].Layout, colorBrush);
            }

            //reset the matrix
            deviceContext.Transform = Matrix3x2.Identity;

            //draw whos playing text
            colorBrush.Color = Color.White;
            deviceContext.DrawTextLayout(new RawVector2(center.X - (whosPlayingLayout.MaxWidth / 2), deviceContext.Size.Height / 2 - (profilePictureSize + whosPlayingLayout.FontSize)), whosPlayingLayout, colorBrush);


            //draw add user button:
            //animate based on selection
            if (currentInputLayer == InputLayers.Utilities && selectedUtilityIndex == 0)
                utilityButtonScales.addUserScale = lerp(utilityButtonScales.addUserScale, utilityButtonSelectedScale, lerpAmount);
            else
                utilityButtonScales.addUserScale = lerp(utilityButtonScales.addUserScale, 1f, lerpAmount);

            //set the color and draw the button
            colorBrush.Color = utilityButtonColor;
            deviceContext.FillEllipse(new Ellipse(new Vector2(utilityButtonPositions.addUserPosition.X, utilityButtonPositions.addUserPosition.Y), utilityButtonSize / 2 * utilityButtonScales.addUserScale, utilityButtonSize / 2 * utilityButtonScales.addUserScale), colorBrush);
            deviceContext.DrawBitmap(utilityButtonImages.addUserImage.Image,new RawRectangleF(utilityButtonPositions.addUserPosition.X - utilityButtonSize / 2 * utilityButtonScales.addUserScale * 0.7f * 1.2f, utilityButtonPositions.addUserPosition.Y - utilityButtonSize / 2 * utilityButtonScales.addUserScale * 0.7f, utilityButtonPositions.addUserPosition.X + utilityButtonSize / 2 * utilityButtonScales.addUserScale * 0.7f * 0.8f, utilityButtonPositions.settingsPosition.Y + utilityButtonSize / 2 * utilityButtonScales.addUserScale * 0.7f) ,1.0f, SharpDX.Direct2D1.BitmapInterpolationMode.Linear);

            //draw the outline
            colorBrush.Color = Color.White;
            deviceContext.StrokeWidth = borderThickness;
            deviceContext.DrawEllipse(new Ellipse(new Vector2(utilityButtonPositions.addUserPosition.X, utilityButtonPositions.addUserPosition.Y), utilityButtonSize / 2 * utilityButtonScales.addUserScale, utilityButtonSize / 2 * utilityButtonScales.addUserScale), colorBrush);

            //draw settings button
            //animate based on selection
            if(currentInputLayer == InputLayers.Utilities && selectedUtilityIndex == 1)
                utilityButtonScales.settingsScale = lerp(utilityButtonScales.settingsScale, utilityButtonSelectedScale, lerpAmount);
            else
                utilityButtonScales.settingsScale = lerp(utilityButtonScales.settingsScale, 1f, lerpAmount);

            //set the color and draw the button
            colorBrush.Color = utilityButtonColor;
            deviceContext.FillEllipse(new Ellipse(new Vector2(utilityButtonPositions.settingsPosition.X, utilityButtonPositions.settingsPosition.Y), utilityButtonSize / 2 * utilityButtonScales.settingsScale, utilityButtonSize / 2 * utilityButtonScales.settingsScale), colorBrush);
            deviceContext.DrawBitmap(utilityButtonImages.settingsImage.Image, new RawRectangleF(utilityButtonPositions.settingsPosition.X - utilityButtonSize / 2 * utilityButtonScales.settingsScale * 0.7f, utilityButtonPositions.settingsPosition.Y - utilityButtonSize / 2 * utilityButtonScales.settingsScale * 0.7f, utilityButtonPositions.settingsPosition.X + utilityButtonSize / 2 * utilityButtonScales.settingsScale * 0.7f, utilityButtonPositions.settingsPosition.Y + utilityButtonSize / 2 * utilityButtonScales.settingsScale * 0.7f), 1f, SharpDX.Direct2D1.BitmapInterpolationMode.Linear);
           
            //draw the outline
            colorBrush.Color = Color.White;
            deviceContext.StrokeWidth = borderThickness;
            deviceContext.DrawEllipse(new Ellipse(new Vector2(utilityButtonPositions.settingsPosition.X, utilityButtonPositions.settingsPosition.Y), utilityButtonSize / 2 * utilityButtonScales.settingsScale, utilityButtonSize / 2 * utilityButtonScales.settingsScale), colorBrush);

            //draw a hint for the input to edit a profile
            if (users.Length > 0)
            {
                colorBrush.Color = Color.White;
                float deviceScale = deviceContext.Size.Height / baseScalingResolution.Height * 0.8f;

                deviceContext.Transform = Matrix3x2.Scaling(deviceScale, deviceScale, new Vector2(0f, deviceContext.Size.Height));

                switch (currentInputType)
                {
                    case InputType.Mouse:
                        deviceContext.DrawBitmap(inputImageMouseRightClick.Image, new RawRectangleF(20f, deviceContext.Size.Height - 100f, 100f, deviceContext.Size.Height - 20f), 1f, SharpDX.Direct2D1.BitmapInterpolationMode.Linear);
                        deviceContext.DrawTextLayout(new Vector2(100f, deviceContext.Size.Height - editProfileInputTextLayout.MaxHeight - 50f), editProfileInputTextLayout, colorBrush);
                        break;

                    case InputType.Keyboard:
                        deviceContext.DrawBitmap(inputImageSpacebar.Image, new RawRectangleF(20f, deviceContext.Size.Height - 70f, 140f, deviceContext.Size.Height - 20f), 1f, SharpDX.Direct2D1.BitmapInterpolationMode.Linear);
                        deviceContext.DrawTextLayout(new Vector2(160f, deviceContext.Size.Height - editProfileInputTextLayout.MaxHeight - 35f), editProfileInputTextLayout, colorBrush);
                        break;

                    case InputType.Controller:
                        deviceContext.DrawBitmap(inputImageXboxYButton.Image, new RawRectangleF(20f, deviceContext.Size.Height - 90f, 90f, deviceContext.Size.Height - 20f), 1f, SharpDX.Direct2D1.BitmapInterpolationMode.Linear);
                        deviceContext.DrawTextLayout(new Vector2(100f, deviceContext.Size.Height - editProfileInputTextLayout.MaxHeight - 40f), editProfileInputTextLayout, colorBrush);
                        break;
                }
            }

            //end the draw call
            deviceContext.EndDraw();
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

        private int clamp(int value, int clampLow, int clampHigh)
        {
            return value < clampLow ? clampLow : value > clampHigh ? clampHigh : value;
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

        private void Dispose()
        {
            utilityButtonImages.addUserImage.Image.Dispose();
            utilityButtonImages.settingsImage.Image.Dispose();

            for (int i = 0; i < users.Length; i++)
            {
                users[i].ProfileGeometry.Dispose();
                users[i].ProfileBrush.Dispose();
                users[i].ProfileImage.Image.Dispose();
                users[i].Layout.Dispose();
            }

            whosPlayingLayout.Dispose();

            editProfileInputTextLayout.Dispose();

            colorBrush.Dispose();
            backgroundGradientBrush.Dispose();

            if (selectionClickVoice != null)
                selectionClickVoice.Dispose();

            audioOut.Dispose();
            profileClickStream.Dispose();

            utilityButtonImages.addUserImage.Image.Dispose();
            utilityButtonImages.settingsImage.Image.Dispose();
            editProfileBitmap.Image.Dispose();
            addUserStrokeStyle.Dispose();
            inputImageMouseRightClick.Image.Dispose();
            inputImageSpacebar.Image.Dispose();
            inputImageXboxYButton.Image.Dispose();

            sw.Stop();
        }
    }
}
