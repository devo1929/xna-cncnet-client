using System;
using ClientGUI;
using DTAClient.ViewModels;
using Rampastring.XNAUI;
using Rampastring.XNAUI.XNAControls;
using ReactiveUI;

namespace DTAClient.DXGUI.Multiplayer.CnCNet
{
    /// <summary>
    /// A panel that hides itself if it's clicked while none of its children
    /// are the focus of input.
    /// </summary>
    public class PrivateMessagingPanel : DarkeningPanel, IViewFor<PrivateMessagingWindowViewModel>
    {
        private readonly PrivateMessagingWindow privateMessagingWindow;

        public PrivateMessagingPanel(
            WindowManager windowManager,
            PrivateMessagingWindow privateMessagingWindow,
            PrivateMessagingWindowViewModel viewModel
        ) : base(windowManager)
        {
            this.privateMessagingWindow = privateMessagingWindow;
            ViewModel = viewModel;
        }

        private void SetVisible(bool visible)
        {
            if (visible)
            {
                Show();
                return;
            }

            Hide();
        }

        public override void OnLeftClick()
        {
            bool hideControl = true;

            foreach (XNAControl child in Children)
            {
                if (child.IsActive)
                {
                    hideControl = false;
                    break;
                }
            }

            if (hideControl)
                ViewModel.Visible = false;

            base.OnLeftClick();
        }

        public override void Initialize()
        {
            base.Initialize();

            AddChild(privateMessagingWindow);

            ViewModel
                .WhenAnyValue(vm => vm.Visible)
                .Subscribe(SetVisible);
        }

        object IViewFor.ViewModel
        {
            get => ViewModel;
            set => ViewModel = (PrivateMessagingWindowViewModel)value;
        }

        public PrivateMessagingWindowViewModel ViewModel { get; set; }
    }
}