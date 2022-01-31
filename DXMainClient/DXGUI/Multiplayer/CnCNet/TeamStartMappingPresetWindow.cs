using System;
using ClientGUI;
using Microsoft.Xna.Framework;
using Rampastring.XNAUI;
using Rampastring.XNAUI.XNAControls;

namespace DTAClient.DXGUI.Multiplayer.CnCNet
{
    public class TeamStartMappingPresetWindow : XNAWindow
    {
        private readonly XNALabel lblHeader;
        private readonly XNATextBox inputPresetName;
        private readonly XNAClientButton btnSave;

        public EventHandler<string> PresetSaved;

        public TeamStartMappingPresetWindow(WindowManager windowManager) : base(windowManager)
        {
            ClientRectangle = new Rectangle(0, 0, 325, 185);

            var margin = 10;

            lblHeader = new XNALabel(WindowManager);
            lblHeader.Name = nameof(lblHeader);
            lblHeader.FontIndex = 1;
            lblHeader.Text = "Save Custom Auto Ally Preset";
            lblHeader.ClientRectangle = new Rectangle(
                margin, margin,
                150, 22
            );

            var lblPresetName = new XNALabel(WindowManager);
            lblPresetName.Name = nameof(lblPresetName);
            lblPresetName.Text = "Preset Name";
            lblPresetName.ClientRectangle = new Rectangle(
                margin, lblHeader.Bottom + margin,
                150, 18
            );

            inputPresetName = new XNATextBox(WindowManager);
            inputPresetName.Name = nameof(inputPresetName);
            inputPresetName.ClientRectangle = new Rectangle(
                10, lblPresetName.Bottom + 2,
                150, 22
            );

            btnSave = new XNAClientButton(WindowManager);
            btnSave.Name = nameof(btnSave);
            btnSave.Text = "Save";
            btnSave.LeftClick += BtnLoadSave_LeftClick;
            btnSave.ClientRectangle = new Rectangle(
                margin,
                Height - UIDesignConstants.BUTTON_HEIGHT - margin,
                UIDesignConstants.BUTTON_WIDTH_92,
                UIDesignConstants.BUTTON_HEIGHT
            );

            var btnCancel = new XNAClientButton(WindowManager);
            btnCancel.Text = "Cancel";
            btnCancel.ClientRectangle = new Rectangle(
                btnSave.Right + margin,
                btnSave.Y,
                UIDesignConstants.BUTTON_WIDTH_92,
                UIDesignConstants.BUTTON_HEIGHT
            );
            btnCancel.LeftClick += (sender, args) => Disable();

            AddChild(lblHeader);
            AddChild(lblPresetName);
            AddChild(inputPresetName);
            AddChild(btnSave);
            AddChild(btnCancel);

            Disable();
        }

        private void BtnLoadSave_LeftClick(object sender, EventArgs e)
        {
            PresetSaved?.Invoke(this, inputPresetName.Text);

            Disable();
        }

        public override void Initialize()
        {
            PanelBackgroundDrawMode = PanelBackgroundImageDrawMode.STRETCHED;
            BackgroundTexture = AssetLoader.CreateTexture(new Color(0, 0, 0, 255), 1, 1);

            base.Initialize();
        }
    }
}
