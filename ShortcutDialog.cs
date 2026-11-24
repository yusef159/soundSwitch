namespace SoundSwitch;

internal sealed class ShortcutDialog : Form
{
    private readonly CheckBox control = new() { Text = "Ctrl", AutoSize = true };
    private readonly CheckBox alt = new() { Text = "Alt", AutoSize = true };
    private readonly CheckBox shift = new() { Text = "Shift", AutoSize = true };
    private readonly CheckBox windows = new() { Text = "Win", AutoSize = true };
    private readonly ComboBox keyPicker = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 108 };
    private readonly Label errorLabel = new() { ForeColor = Color.Firebrick, AutoSize = true };

    internal ShortcutDialog(Hotkey current, Func<Hotkey, string?> save)
    {
        Text = "Keyboard shortcut";
        AutoScaleMode = AutoScaleMode.Dpi;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterScreen;
        ShowInTaskbar = false;
        MinimizeBox = false;
        MaximizeBox = false;
        Font = SystemFonts.MessageBoxFont;

        control.Checked = current.Modifiers.HasFlag(HotkeyModifiers.Control);
        alt.Checked = current.Modifiers.HasFlag(HotkeyModifiers.Alt);
        shift.Checked = current.Modifiers.HasFlag(HotkeyModifiers.Shift);
        windows.Checked = current.Modifiers.HasFlag(HotkeyModifiers.Windows);

        keyPicker.AccessibleName = "Shortcut key";
        keyPicker.FormattingEnabled = true;
        keyPicker.Format += (_, args) => args.Value = Hotkey.KeyLabel((Keys)args.ListItem!);
        foreach (var range in new[] { (Keys.A, Keys.Z), (Keys.D0, Keys.D9), (Keys.F1, Keys.F24) })
        {
            for (Keys key = range.Item1; key <= range.Item2; key++)
            {
                if (key != Keys.F12)
                    keyPicker.Items.Add(key);
            }
        }
        keyPicker.SelectedItem = current.Key;

        var saveButton = new Button { Text = "Save", AutoSize = true };
        var cancelButton = new Button { Text = "Cancel", AutoSize = true, DialogResult = DialogResult.Cancel };
        saveButton.Click += (_, _) =>
        {
            HotkeyModifiers modifiers = 0;
            if (control.Checked) modifiers |= HotkeyModifiers.Control;
            if (alt.Checked) modifiers |= HotkeyModifiers.Alt;
            if (shift.Checked) modifiers |= HotkeyModifiers.Shift;
            if (windows.Checked) modifiers |= HotkeyModifiers.Windows;
            var shortcut = new Hotkey(modifiers, (Keys)keyPicker.SelectedItem!);
            string? error = shortcut.IsValid ? save(shortcut) : "Choose at least one modifier key.";
            if (error is null)
                DialogResult = DialogResult.OK;
            else
                errorLabel.Text = error;
        };

        var modifiersRow = new FlowLayoutPanel { AutoSize = true, WrapContents = false };
        modifiersRow.Controls.AddRange([control, alt, shift, windows]);
        var buttons = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Anchor = AnchorStyles.Right };
        buttons.Controls.AddRange([saveButton, cancelButton]);
        var layout = new TableLayoutPanel { AutoSize = true, ColumnCount = 1, Padding = new Padding(14), Dock = DockStyle.Fill };
        layout.Controls.Add(new Label { Text = "Choose modifier keys and a shortcut key.", AutoSize = true });
        layout.Controls.Add(modifiersRow);
        layout.Controls.Add(keyPicker);
        layout.Controls.Add(errorLabel);
        layout.Controls.Add(buttons);
        Controls.Add(layout);
        AcceptButton = saveButton;
        CancelButton = cancelButton;
    }
}
