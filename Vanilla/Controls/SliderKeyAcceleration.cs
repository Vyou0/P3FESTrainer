using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace P3FESTrainer.Controls
{
    public static class SliderKeyAcceleration
    {
        public static readonly DependencyProperty EnableProperty =
            DependencyProperty.RegisterAttached(
                "Enable",
                typeof(bool),
                typeof(SliderKeyAcceleration),
                new PropertyMetadata(false, OnEnableChanged));

        public static bool GetEnable(DependencyObject obj) => (bool)obj.GetValue(EnableProperty);
        public static void SetEnable(DependencyObject obj, bool value) => obj.SetValue(EnableProperty, value);

        private static void OnEnableChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is Slider slider)
            {
                if ((bool)e.NewValue)
                {
                    slider.PreviewKeyDown += Slider_PreviewKeyDown;
                    slider.PreviewKeyUp += Slider_PreviewKeyUp;
                    slider.LostFocus += Slider_LostFocus;
                    slider.PreviewMouseDown += Slider_PreviewMouseDown;
                    slider.Focusable = true;
                    slider.IsTabStop = true;
                }
                else
                {
                    slider.PreviewKeyDown -= Slider_PreviewKeyDown;
                    slider.PreviewKeyUp -= Slider_PreviewKeyUp;
                    slider.LostFocus -= Slider_LostFocus;
                    slider.PreviewMouseDown -= Slider_PreviewMouseDown;
                }
            }
        }

        private class State
        {
            public Slider Slider;
            public Key HeldKey;
            public Stopwatch Stopwatch = new();
            public DispatcherTimer Timer = new();

            public State(Slider slider)
            {
                Slider = slider;
                Timer.Interval = TimeSpan.FromMilliseconds(20);
                Timer.Tick += OnTick;
            }

            private void OnTick(object? sender, EventArgs e)
            {
                if (!Slider.IsFocused && !Slider.IsKeyboardFocusWithin)
                {
                    Stop();
                    return;
                }

                double elapsed = Stopwatch.Elapsed.TotalSeconds;
                double range = Slider.Maximum - Slider.Minimum;
                double baseStep = Math.Max(1.0, Slider.SmallChange);

                double step = baseStep;
                if (elapsed > 0.2)
                {
                    double timeFactor = Math.Pow(elapsed - 0.2, 1.8);
                    double speedMult = Math.Max(1.5, range / 15.0);
                    step = Math.Min(Math.Max(5.0, range / 5.0), baseStep + timeFactor * speedMult);
                }

                StepSlider(Slider, HeldKey, step);
            }

            public void Start(Key key)
            {
                HeldKey = key;
                Stopwatch.Restart();
                Timer.Start();
            }

            public void Stop()
            {
                Timer.Stop();
                Stopwatch.Stop();
            }
        }

        private static readonly DependencyProperty StateProperty =
            DependencyProperty.RegisterAttached("State", typeof(State), typeof(SliderKeyAcceleration), new PropertyMetadata(null));

        private static void Slider_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is Slider slider)
            {
                slider.Focus();
            }
        }

        private static void Slider_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (sender is not Slider slider) return;

            if (e.Key != Key.Left && e.Key != Key.Right && e.Key != Key.Up && e.Key != Key.Down)
                return;

            e.Handled = true;

            var state = (State?)slider.GetValue(StateProperty);
            if (state == null)
            {
                state = new State(slider);
                slider.SetValue(StateProperty, state);
            }

            if (state.Timer.IsEnabled)
            {
                if (state.HeldKey == e.Key) return;
                state.Stop();
            }

            // Perform initial step immediately
            double baseStep = Math.Max(1.0, slider.SmallChange);
            StepSlider(slider, e.Key, baseStep);

            state.Start(e.Key);
        }

        private static void Slider_PreviewKeyUp(object sender, KeyEventArgs e)
        {
            if (sender is not Slider slider) return;

            if (e.Key != Key.Left && e.Key != Key.Right && e.Key != Key.Up && e.Key != Key.Down)
                return;

            var state = (State?)slider.GetValue(StateProperty);
            if (state != null && state.HeldKey == e.Key)
            {
                state.Stop();
                e.Handled = true;
            }
        }

        private static void Slider_LostFocus(object sender, RoutedEventArgs e)
        {
            if (sender is not Slider slider) return;
            var state = (State?)slider.GetValue(StateProperty);
            if (state != null)
            {
                state.Stop();
            }
        }

        private static void StepSlider(Slider slider, Key key, double step)
        {
            if (key == Key.Right || key == Key.Up)
            {
                slider.Value = Math.Min(slider.Maximum, slider.Value + step);
            }
            else if (key == Key.Left || key == Key.Down)
            {
                slider.Value = Math.Max(slider.Minimum, slider.Value - step);
            }
        }
    }
}
