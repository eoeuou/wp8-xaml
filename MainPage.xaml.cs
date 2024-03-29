﻿using System;
using System.ComponentModel;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using Microsoft.Phone.Controls;
using PhoneDirect3DXamlAppComponent;
using System.Threading;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.Phone.Shell;
using Windows.UI.Input;
using System.Threading;
using Microsoft.Phone.Info;
using Microsoft.Phone.Net.NetworkInformation;
using System.Windows.Threading;
using System.IO.IsolatedStorage;  

namespace PhoneDirect3DXamlAppInterop
{
    public partial class MainPage : PhoneApplicationPage
    {
        private Direct3DInterop m_d3dInterop = null;

        // event handler for CCEditBox
        private event EventHandler<String> m_receiveHandler;

        // invisible XAML TextBox for Cocos2d-x keyboard input
        TextBox m_textBox = null;

        //datamanager
        private WP8DataManager m_dataManager = null;

        // Constructor
        public MainPage()
        {
            InitializeComponent();
        }

        private void DrawingSurface_Loaded(object sender, RoutedEventArgs e)
        {
            if (m_d3dInterop == null)
            {
                m_d3dInterop = new Direct3DInterop();

                // Set WindowBounds to size of DrawingSurface
                m_d3dInterop.WindowBounds = new Windows.Foundation.Size(
                    (float)DrawingSurface.ActualWidth,
                    (float)DrawingSurface.ActualHeight
                    );

                // Hook-up native component to DrawingSurface
                DrawingSurface.SetContentProvider(m_d3dInterop.CreateContentProvider());
                DrawingSurface.SetManipulationHandler(m_d3dInterop);

                // Hook-up Cocos2d-x delegates
                m_d3dInterop.SetCocos2dEventDelegate(OnCocos2dEvent);
                m_d3dInterop.SetCocos2dMessageBoxDelegate(OnCocos2dMessageBoxEvent);
                m_d3dInterop.SetCocos2dEditBoxDelegate(OpenEditBox);

                MemoryDisplay.Visibility = Visibility.Visible;
                StartTimer();
            }


            if (m_dataManager == null)
            {
                m_dataManager = new WP8DataManager();
                m_dataManager.SetGetUniqueIDDelegate(getUniqueID);
            }
            
            m_d3dInterop.OnWP8RedeemResult(WP8RedeemEventType.WP8RedeemSuccess,"110");
        }
        #region memory

        private DispatcherTimer m_timer = null;
        private void StartTimer()
        {
            m_timer = new DispatcherTimer();
            m_timer.Interval = new TimeSpan(0, 0, 1);
            m_timer.Tick += new EventHandler(TimerTick);
            m_timer.Start();
        }
        private void TimerTick(object sender, EventArgs e)
        {
            try
            {
                // These are TextBlock controls that are created in the page’s XAML file.  
                float value = DeviceStatus.ApplicationCurrentMemoryUsage / (1024.0f * 1024.0f);
                MemoryTextBlock.Text = value.ToString();
                value = DeviceStatus.ApplicationPeakMemoryUsage / (1024.0f * 1024.0f);
                PeakMemoryTextBlock.Text = value.ToString();

                value = DeviceStatus.ApplicationMemoryUsageLimit / (1024.0f * 1024.0f);
                MemoryUsageLimiTextBlock.Text = value.ToString();

                value = DeviceStatus.DeviceTotalMemory / (1024.0f * 1024.0f);
                TotalMemoryTextBlock.Text = value.ToString();
            }
            catch (Exception ex)
            {
                MemoryTextBlock.Text = ex.Message;
            }
        }
        #endregion
        // called when the user presses the back button on the device
        protected override void OnBackKeyPress(CancelEventArgs e)
        {
            m_d3dInterop.OnBackKeyPress();
            // cocos2d-x will async send Cocos2dEvent.TerminateApp event if it is time to exit app.
            // We do not want to exit now, so we set e.Cancel to true.
            e.Cancel = true;
        }

        public void OnKeyDown(object sender, KeyEventArgs e)
        {
            ModifierKeys modifiers = Keyboard.Modifiers;

            switch (e.Key)
            {
                case Key.Escape:
                    m_d3dInterop.OnCocos2dKeyEvent(Cocos2dKeyEvent.Escape);
                    e.Handled = true;
                    break;
                case Key.Back:
                    m_d3dInterop.OnCocos2dKeyEvent(Cocos2dKeyEvent.Back);
                    e.Handled = true;
                    break;
                case Key.Enter:
                    m_d3dInterop.OnCocos2dKeyEvent(Cocos2dKeyEvent.Enter);
                    e.Handled = true;
                    break;
                default:
                    break;
            }
        }

        public void OnKeyUp(object sender, KeyEventArgs e)
        {
            m_d3dInterop.OnCocos2dKeyEvent(Cocos2dKeyEvent.Text, m_textBox.Text);
            m_textBox.Text = "";
        }

        // Called by the Cocos2d-x C++ engine to display a MessageBox
        public void OnCocos2dMessageBoxEvent(String title, String text)
        {
            Dispatcher.BeginInvoke(() =>
            {
                MessageBox.Show(text, title, MessageBoxButton.OK);
            });
        }

        // events called by the Cocos2d-x C++ engine to be handled by C#
        public void OnCocos2dEvent(Cocos2dEvent theEvent)
        {
            Dispatcher.BeginInvoke(() =>
            {
                switch (theEvent)
                {
                    case Cocos2dEvent.TerminateApp:
                        Application.Current.Terminate();
                        break;

                    case Cocos2dEvent.ShowKeyboard:
                        if (m_textBox == null)
                        {
                            m_textBox = new TextBox();
                            m_textBox.Opacity = 0.0;
                            m_textBox.Width = 1;
                            m_textBox.Height = 1;
                            m_textBox.MaxLength = 1;
                            m_textBox.KeyDown += OnKeyDown;
                            m_textBox.KeyUp += OnKeyUp;
                            LayoutRoot.Children.Add(m_textBox);
                        }
                        m_textBox.Focus();
                        break;

                    case Cocos2dEvent.HideKeyboard:
                        if (m_textBox != null)
                        {
                            LayoutRoot.Children.Remove(m_textBox);
                        }
                        m_textBox = null;
                        break;
                }
            });
        }

        // Called by the Cocos2d-x C++ engine to display a CCEditBox
        public void OpenEditBox(String strPlaceHolder, string strText, int maxLength, int inputMode, int inputFlag, EventHandler<String> receiveHandler)
        {
            m_receiveHandler = receiveHandler;
            Deployment.Current.Dispatcher.BeginInvoke(() =>
            {
                EditBox editbox = new EditBox(this, strPlaceHolder, strText, maxLength, inputMode, inputFlag);
                LayoutRoot.Children.Add(editbox);
            });
        }

        public void OnSelectText(object sender, String str)
        {
            if (m_d3dInterop != null && m_receiveHandler != null)
            {
                m_d3dInterop.OnCocos2dEditboxEvent(sender, str, m_receiveHandler);
            }
        }

        public String getUniqueID()
        {
            /*try {
                byte[] uniqueIDbytes = (byte[])DeviceExtendedProperties.GetValue("DeviceUniqueId");
                string uniqueID = System.Convert.ToBase64String(uniqueIDbytes);
            }catch(Exception ex){
                MessageBox.Show(ex.Message, "Failed", MessageBoxButton.OK);
            }*/

            float
                value = DeviceStatus.ApplicationPeakMemoryUsage / (1024.0f * 1024.0f);
            String r = value.ToString();
            return r;
        }

    }
}