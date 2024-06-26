﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Media;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using ClassLibraryCommon;
using BIOSBuddy.Events;
using BIOSBuddy.Interfaces;
using BIOSBuddy.Windows;
using DCS_BIOS;
using DCS_BIOS.ControlLocator;
using DCS_BIOS.Json;
using DCS_BIOS.Serialized;

namespace BIOSBuddy.UserControls
{
    /// <summary>
    /// Interaction logic for DCSBIOSControlUserControl.xaml
    /// </summary>
    public partial class DCSBIOSControlUserControl : IDisposable, INewDCSBIOSData
    {
        private readonly DCSBIOSControl _dcsbiosControl;
        private ToolTip _copyToolTip;
        private readonly List<DCSBIOSOutput> _dcsbiosOutputs = new();
        private bool _isLoaded;
        private readonly string _luaCommand;
        private bool _textBoxDataFocused;
        private bool _sendDataArrayRunning;
        private Thread _sendDataThread;

        public DCSBIOSControlUserControl(DCSBIOSControl dcsbiosControl, string luaCommand)
        {
            InitializeComponent();

            Name = dcsbiosControl.Identifier;
            _dcsbiosControl = dcsbiosControl;
            _luaCommand = luaCommand;
            foreach (var dcsbiosControlOutput in dcsbiosControl.Outputs)
            {
                _dcsbiosOutputs.Add(DCSBIOSControlLocator.GetDCSBIOSOutput(_dcsbiosControl.Identifier, dcsbiosControlOutput.OutputDataType));
            }

            REFEventHandler.AttachDCSBIOSDataListener(this);
        }

        private void Dispose(bool disposing)
        {
            if (disposing)
            {
                REFEventHandler.DetachDCSBIOSDataListener(this);
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void DCSBIOSControlUserControl_OnLoaded(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_isLoaded) return;
                _isLoaded = true;

                ShowControl();
                SetFormState();
            }
            catch (Exception ex)
            {
                Common.ShowErrorMessageBox(ex);
            }
        }

        private void SetFormState()
        {
            try
            {
                ButtonSetVariableStep.IsEnabled = !string.IsNullOrEmpty(TextBoxVariableStepValue.Text) && int.TryParse(TextBoxVariableStepValue.Text, out _);
                ButtonSetString.IsEnabled = !string.IsNullOrEmpty(TextBoxSetStringValue.Text);
                ButtonSetVariableIncrease.IsEnabled = ButtonSetVariableStep.IsEnabled;
                ButtonSetVariableDecrease.IsEnabled = ButtonSetVariableStep.IsEnabled;
                LabelShowSendData.Visibility = _dcsbiosControl.Inputs.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
                ButtonSendData.IsEnabled = _textBoxDataFocused && !string.IsNullOrEmpty(TextBoxDataToSend.Text) && _dcsbiosControl.Inputs.Count > 0;
                
                ComboBoxSendDelay.IsEnabled = ButtonSendData.IsEnabled && !_sendDataArrayRunning;
                LabelDelay.IsEnabled = ButtonSendData.IsEnabled;
            }
            catch (Exception ex)
            {
                Common.ShowErrorMessageBox(ex);
            }
        }

        public void NewDCSBIOSData(object sender, DCSBIOSDataCombinedEventArgs args)
        {

            if (_dcsbiosOutputs.Any(o => o.Address == args.Address) == false) return;

            foreach (var dcsbiosOutput in _dcsbiosOutputs)
            {
                if (dcsbiosOutput.Address == args.Address && args.IsUIntValue)
                {
                    SetUintValue(dcsbiosOutput.GetUIntValue(args.Data));
                    break;
                }

                if (dcsbiosOutput.Address == args.Address && !args.IsUIntValue)
                {
                    SetStringValue(args.StringValue);
                }
            }
        }

        private void SetUintValue(uint value)
        {
            try
            {
                Dispatcher?.BeginInvoke((Action)(() => LabelCurrentUIntValue.Content = Convert.ToString(value)));

                // Setting this will interfere when user is setting its own value, if the update cycle is fast the user
                // won't be able to test at all
                // Dispatcher?.BeginInvoke((Action)(() => SetSliderValue(value)));

                if (_dcsbiosOutputs.Any(o => o.DCSBiosOutputType == DCSBiosOutputType.IntegerType) == false)
                {
                    return;
                }

                var dcsbiosOutput = _dcsbiosOutputs.First(o => o.DCSBiosOutputType == DCSBiosOutputType.IntegerType);
                if (dcsbiosOutput.MaxValue == 0)
                {
                    return;
                }
                var percentage = value * 100 / dcsbiosOutput.MaxValue;
                Dispatcher?.BeginInvoke((Action)(() => LabelPercentage.Content = $"({percentage}%)"));
            }
            catch (Exception ex)
            {
                Common.ShowErrorMessageBox(ex);
            }
        }

        private void SetStringValue(string value)
        {
            try
            {
                Dispatcher?.BeginInvoke((Action)(() => LabelCurrentStringValue.Content = $"->{value}<-"));
            }
            catch (Exception ex)
            {
                Common.ShowErrorMessageBox(ex);
            }
        }

        /*
        private void SetSliderValue(uint value)
        {
            try
            {
                if (SliderSetState.Visibility != Visibility.Visible) return;

                if (!(Math.Abs(SliderSetState.Value - Convert.ToDouble(value)) > 0.5)) return;

                SliderSetState.ValueChanged -= SliderSetState_OnValueChanged;
                SliderSetState.Value = value;
                SliderSetState.ValueChanged += SliderSetState_OnValueChanged;

                ButtonSetState.Content = value;
            }
            catch (Exception ex)
            {
                Common.ShowErrorMessageBox(ex);
            }
        }
        */

        private void ShowControl()
        {
            try
            {
                LabelLuaInformation.Visibility = string.IsNullOrEmpty(_luaCommand) ? Visibility.Collapsed : Visibility.Visible;

                LabelControlId.Content = _dcsbiosControl.Identifier.Replace("_", "__");
                LabelCategory.Content = _dcsbiosControl.Category;
                LabelDescription.Content = _dcsbiosControl.Description;

                StackPanelFixedStep.Visibility = Visibility.Collapsed;
                StackPanelVariableStep.Visibility = Visibility.Collapsed;
                StackPanelSetState.Visibility = Visibility.Collapsed;
                StackPanelAction.Visibility = Visibility.Collapsed;
                StackPanelSetString.Visibility = Visibility.Collapsed;
                StackPanelSendData.Visibility = Visibility.Collapsed;

                foreach (var dcsbiosControlInput in _dcsbiosControl.Inputs)
                {
                    var inputInterface = dcsbiosControlInput.GetInputInterface();
                    switch (inputInterface.Interface)
                    {
                        case DCSBIOSInputType.FIXED_STEP:
                            {
                                StackPanelFixedStep.Visibility = Visibility.Visible;
                                break;
                            }
                        case DCSBIOSInputType.VARIABLE_STEP:
                            {
                                StackPanelVariableStep.Visibility = Visibility.Visible;
                                TextBoxVariableStepValue.Text = dcsbiosControlInput.SuggestedStep.ToString();
                                break;
                            }
                        case DCSBIOSInputType.SET_STATE:
                            {
                                StackPanelSetState.Visibility = Visibility.Visible;
                                SliderSetState.Minimum = 0;
                                SliderSetState.Maximum = Convert.ToDouble(dcsbiosControlInput.MaxValue);
                                ButtonSetState.Content = 0;
                                SliderSetState.Value = 0;
                                break;
                            }
                        case DCSBIOSInputType.ACTION:
                            {
                                StackPanelAction.Visibility = Visibility.Visible;
                                break;
                            }
                        case DCSBIOSInputType.SET_STRING:
                            {
                                StackPanelSetString.Visibility = Visibility.Visible;
                                break;
                            }
                        default:
                            {
                                throw new Exception(
                                    $"Failed to identify Input Interface ->{dcsbiosControlInput.ControlInterface}<-.");
                            }
                    }
                }

                StackPanelUInt.Visibility = Visibility.Collapsed;
                StackPanelString.Visibility = Visibility.Collapsed;

                foreach (var dcsbiosOutput in _dcsbiosOutputs)
                {
                    if (dcsbiosOutput.DCSBiosOutputType == DCSBiosOutputType.IntegerType)
                    {
                        StackPanelUInt.Visibility = Visibility.Visible;
                        LabelOutputMaxValue.Content = dcsbiosOutput.MaxValue;
                    }
                    else if (dcsbiosOutput.DCSBiosOutputType == DCSBiosOutputType.StringType)
                    {
                        StackPanelString.Visibility = Visibility.Visible;
                        LabelOutputMaxLength.Content = dcsbiosOutput.MaxLength;
                    }
                }

                if (_dcsbiosOutputs.Count > 1)
                {
                    StackPanelUInt.Background = new SolidColorBrush(Colors.WhiteSmoke);
                }
            }
            catch (Exception ex)
            {
                Common.ShowErrorMessageBox(ex);
            }
        }

        private void UIElement_OnMouseEnter(object sender, MouseEventArgs e)
        {
            try
            {
                Mouse.OverrideCursor = Cursors.Hand;
            }
            catch (Exception ex)
            {
                Common.ShowErrorMessageBox(ex);
            }
        }

        private void UIElement_OnMouseLeave(object sender, MouseEventArgs e)
        {
            try
            {
                Mouse.OverrideCursor = Cursors.Arrow;
                if (_copyToolTip != null)
                {
                    _copyToolTip.IsOpen = false;
                }
            }
            catch (Exception ex)
            {
                Common.ShowErrorMessageBox(ex);
            }
        }

        private void LabelControlId_OnMouseDown(object sender, MouseButtonEventArgs e)
        {
            try
            {
                Clipboard.SetText(_dcsbiosControl.Identifier);
                SystemSounds.Exclamation.Play();
                _copyToolTip = new ToolTip
                {
                    Content = "Value copied.",
                    PlacementTarget = LabelControlId,
                    Placement = PlacementMode.Bottom,
                    IsOpen = true
                };
            }
            catch (Exception ex)
            {
                Common.ShowErrorMessageBox(ex);
            }
        }

        private void LabelControlDescription_OnMouseDown(object sender, MouseButtonEventArgs e)
        {
            try
            {
                Clipboard.SetText(_dcsbiosControl.Description);
                SystemSounds.Exclamation.Play();
                _copyToolTip = new ToolTip
                {
                    Content = "Value copied.",
                    PlacementTarget = LabelControlId,
                    Placement = PlacementMode.Bottom,
                    IsOpen = true
                };
            }
            catch (Exception ex)
            {
                Common.ShowErrorMessageBox(ex);
            }
        }

        private void SliderSetState_OnValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            try
            {
                ButtonSetState.Content = e.NewValue;
            }
            catch (Exception ex)
            {
                Common.ShowErrorMessageBox(ex);
            }
        }

        private void ButtonFixedStepDec_OnClick(object sender, RoutedEventArgs e)
        {
            try
            {
                DCSBIOS.Send($"{_dcsbiosControl.Identifier} DEC\n");
            }
            catch (Exception ex)
            {
                Common.ShowErrorMessageBox(ex);
            }
        }

        private void ButtonFixedStepInc_OnClick(object sender, RoutedEventArgs e)
        {
            try
            {
                DCSBIOS.Send($"{_dcsbiosControl.Identifier} INC\n");
            }
            catch (Exception ex)
            {
                Common.ShowErrorMessageBox(ex);
            }
        }

        private void ButtonSetState_OnClick(object sender, RoutedEventArgs e)
        {
            try
            {
                DCSBIOS.Send($"{_dcsbiosControl.Identifier} {Convert.ToUInt32(SliderSetState.Value)}\n");
            }
            catch (Exception ex)
            {
                Common.ShowErrorMessageBox(ex);
            }
        }

        private void ButtonSetVariableStep_OnClick(object sender, RoutedEventArgs e)
        {
            try
            {
                DCSBIOS.Send($"{_dcsbiosControl.Identifier} {TextBoxVariableStepValue.Text}\n");
            }
            catch (Exception ex)
            {
                Common.ShowErrorMessageBox(ex);
            }
        }

        private void ButtonSetString_OnClick(object sender, RoutedEventArgs e)
        {
            try
            {
                DCSBIOS.Send($"{_dcsbiosControl.Identifier} {TextBoxSetStringValue.Text}\n");
            }
            catch (Exception ex)
            {
                Common.ShowErrorMessageBox(ex);
            }
        }

        private void ButtonSetVariableIncrease_OnClick(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!int.TryParse(TextBoxVariableStepValue.Text, out var changeValue))
                {
                    return;
                }
                DCSBIOS.Send($"{_dcsbiosControl.Identifier} +{Math.Abs(changeValue)}\n");
            }
            catch (Exception ex)
            {
                Common.ShowErrorMessageBox(ex);
            }
        }

        private void ButtonSetVariableDecrease_OnClick(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!int.TryParse(TextBoxVariableStepValue.Text, out var changeValue))
                {
                    return;
                }
                DCSBIOS.Send($"{_dcsbiosControl.Identifier} -{Math.Abs(changeValue)}\n");
            }
            catch (Exception ex)
            {
                Common.ShowErrorMessageBox(ex);
            }
        }

        private void TextBoxVariableStepValue_OnKeyDown(object sender, KeyEventArgs e)
        {
            try
            {
                /*if (e.Key is not (>= Key.D0 and <= Key.D9 or >= Key.NumPad0 and <= Key.NumPad9) && e.Key != Key.OemMinus && e.Key != Key.OemPlus
                    && e.Key != Key.Add && e.Key != Key.Subtract)
                {
                    e.Handled = true;
                    return;
                }*/
                SetFormState();
            }
            catch (Exception ex)
            {
                Common.ShowErrorMessageBox(ex);
            }
        }

        private void TextBoxVariableStepValue_OnKeyUp(object sender, KeyEventArgs e)
        {
            try
            {
                SetFormState();
            }
            catch (Exception ex)
            {
                Common.ShowErrorMessageBox(ex);
            }
        }

        private void ButtonToggle_OnClick(object sender, RoutedEventArgs e)
        {
            try
            {
                DCSBIOS.Send($"{_dcsbiosControl.Identifier} TOGGLE\n");
            }
            catch (Exception ex)
            {
                Common.ShowErrorMessageBox(ex);
            }
        }

        private void LabelCategory_OnMouseEnter(object sender, MouseEventArgs e)
        {
            try
            {
                Mouse.OverrideCursor = Cursors.Hand;
            }
            catch (Exception ex)
            {
                Common.ShowErrorMessageBox(ex);
            }
        }

        private void LabelCategory_OnMouseLeave(object sender, MouseEventArgs e)
        {
            try
            {
                Mouse.OverrideCursor = Cursors.Arrow;
            }
            catch (Exception ex)
            {
                Common.ShowErrorMessageBox(ex);
            }
        }

        private void LabelCategory_OnMouseDown(object sender, MouseButtonEventArgs e)
        {
            try
            {
                REFEventHandler.ChangeCategory(this, LabelCategory.Content.ToString());
            }
            catch (Exception ex)
            {
                Common.ShowErrorMessageBox(ex);
            }
        }

        private void Label_OnMouseEnter(object sender, MouseEventArgs e)
        {
            try
            {
                Mouse.OverrideCursor = Cursors.Hand;
            }
            catch (Exception ex)
            {
                Common.ShowErrorMessageBox(ex);
            }
        }

        private void Label_OnMouseLeave(object sender, MouseEventArgs e)
        {
            try
            {
                Mouse.OverrideCursor = Cursors.Arrow;
            }
            catch (Exception ex)
            {
                Common.ShowErrorMessageBox(ex);
            }
        }

        private void LabelArduinoInformation_OnMouseDown(object sender, MouseButtonEventArgs e)
        {
            try
            {
                var window = new ArduinoWindow(_dcsbiosControl)
                {
                    MaxHeight = 600,
                    SizeToContent = SizeToContent.WidthAndHeight
                };
                var pos = GetPosition();
                window.WindowStartupLocation = WindowStartupLocation.Manual;
                window.Top = pos.Y;
                window.Left = pos.X;
                window.Topmost = true;
                window.ShowDialog();
            }
            catch (Exception ex)
            {
                Common.ShowErrorMessageBox(ex);
            }
        }

        private Point GetPosition()
        {
            // Get absolute location on screen of upper left corner of the UserControl
            var locationFromScreen = PointToScreen(new Point(0, 0));

            // Transform screen point to WPF device independent point
            var source = PresentationSource.FromVisual(this);
            if (source == null || source.CompositionTarget == null) return new Point(0, 0);
            var targetPoints = source.CompositionTarget.TransformFromDevice.Transform(locationFromScreen);
            return targetPoints;
        }

        private void LabelLuaInformation_OnMouseDown(object sender, MouseButtonEventArgs e)
        {
            try
            {
                var window = new LuaWindow(_dcsbiosControl.Identifier, _luaCommand)
                {
                    MaxHeight = 600,
                    SizeToContent = SizeToContent.WidthAndHeight
                };
                var pos = GetPosition();
                window.WindowStartupLocation = WindowStartupLocation.Manual;
                window.Top = pos.Y;
                window.Left = pos.X;
                window.Topmost = true;
                window.ShowDialog();
            }
            catch (Exception ex)
            {
                Common.ShowErrorMessageBox(ex);
            }
        }

        private void TextBoxDataToSend_OnKeyDown(object sender, KeyEventArgs e)
        {
            try
            {
                SetFormState();
            }
            catch (Exception ex)
            {
                Common.ShowErrorMessageBox(ex);
            }
        }

        private void ButtonSendData_OnClick(object sender, RoutedEventArgs e)
        {
            try
            {
                var commands = TextBoxDataToSend.Text;
                var delay = int.Parse(ComboBoxSendDelay.SelectedValue.ToString() ?? "500");
                _sendDataThread = new Thread(() => SendData(commands, delay));
                _sendDataThread.Start();
                SetFormState();
            }
            catch (Exception ex)
            {
                Common.ShowErrorMessageBox(ex);
            }
        }

        private void LabelShowSendData_OnMouseDown(object sender, MouseButtonEventArgs e)
        {
            try
            {
                StackPanelSendData.Visibility = StackPanelSendData.Visibility == Visibility.Collapsed ? StackPanelSendData.Visibility = Visibility.Visible : Visibility.Collapsed;
                SetFormState();
            }
            catch (Exception ex)
            {
                Common.ShowErrorMessageBox(ex);
            }
        }

        private void TextBoxDataToSend_OnGotFocus(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_textBoxDataFocused) return;

                TextBoxDataToSend.Text = "";
                _textBoxDataFocused = true;

                foreach (var dcsbiosInput in _dcsbiosControl.Inputs)
                {
                    var inputInterface = dcsbiosInput.GetInputInterface();
                    switch (inputInterface.Interface)
                    {
                        case DCSBIOSInputType.FIXED_STEP:
                            {
                                TextBoxDataToSend.Text += $"{_dcsbiosControl.Identifier} {DCSBIOSFixedStepInput.INC}\n";
                                TextBoxDataToSend.Text += $"{_dcsbiosControl.Identifier} {DCSBIOSFixedStepInput.DEC}\n";
                                break;
                            }
                        case DCSBIOSInputType.SET_STATE:
                            {
                                if (inputInterface.MaxValue > 10)
                                {
                                    TextBoxDataToSend.Text += $"{_dcsbiosControl.Identifier} {0}\n";
                                    TextBoxDataToSend.Text += $"{_dcsbiosControl.Identifier} {inputInterface.MaxValue/2}\n";
                                    TextBoxDataToSend.Text += $"{_dcsbiosControl.Identifier} {inputInterface.MaxValue}\n";
                                    break;
                                }
                                for (var i = 0; i <= inputInterface.MaxValue; i++)
                                {
                                    TextBoxDataToSend.Text += $"{_dcsbiosControl.Identifier} {i}\n";
                                }
                                break;
                            }
                        case DCSBIOSInputType.ACTION:
                            {
                                TextBoxDataToSend.Text += $"{_dcsbiosControl.Identifier} TOGGLE\n";
                                break;
                            }
                        case DCSBIOSInputType.VARIABLE_STEP:
                            {
                                TextBoxDataToSend.Text += $"{_dcsbiosControl.Identifier} +3200\n";
                                TextBoxDataToSend.Text += $"{_dcsbiosControl.Identifier} -3200\n";
                                break;
                            }
                        case DCSBIOSInputType.SET_STRING:
                            {
                                TextBoxDataToSend.Text += $"{_dcsbiosControl.Identifier} ABCDEF\n";
                                break;
                            }
                        default:
                            throw new ArgumentOutOfRangeException(nameof(inputInterface.Interface));
                    }
                }
                SetFormState();
            }
            catch (Exception ex)
            {
                Common.ShowErrorMessageBox(ex);
            }
        }

        private void SendData(string commands, int delay)
        {
            try
            {
                _sendDataArrayRunning = true;
                var commandArray = commands.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                foreach (var command in commandArray)
                {
                    Dispatcher?.Invoke(() => TextBoxExecutedCommand.Text = command.Replace("\n", "").Replace("\r", ""));
                    DCSBIOS.Send(command + "\n");
                    Dispatcher?.Invoke(SetFormState);
                    Thread.Sleep(delay);
                }
            }
            catch (Exception ex)
            {
                Common.ShowErrorMessageBox(ex);
            }

            _sendDataArrayRunning = false;
            SystemSounds.Beep.Play();
            Dispatcher?.Invoke(SetFormState);
        }

        private void TextBoxDataToSend_OnPreviewKeyUp(object sender, KeyEventArgs e)
        {
            try
            {
                SetFormState();
            }
            catch (Exception ex)
            {
                Common.ShowErrorMessageBox(ex);
            }
        }
    }
}
