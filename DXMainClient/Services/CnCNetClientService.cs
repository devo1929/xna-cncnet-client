using System;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using ClientGUI;
using DTAClient.Enums;
using DTAClient.ViewModels;

namespace DTAClient.Services;

public class CnCNetClientService
{
    private readonly BehaviorSubject<ClientViewModel> clientViewModelSubject = new(new ClientViewModel());

    private ClientViewModel viewModel => clientViewModelSubject.Value;

    public CnCNetClientService()
    {
        GameProcessLogic.GameProcessStarted += () => SetGameProcessState(GameProcessStateEnum.Started);
        GameProcessLogic.GameProcessStarting += () => SetGameProcessState(GameProcessStateEnum.Staring);
        GameProcessLogic.GameProcessExited += () => SetGameProcessState(GameProcessStateEnum.Exited);
    }

    public IObservable<ClientViewModel> GetViewModel() => clientViewModelSubject.AsObservable();

    private void SetGameProcessState(GameProcessStateEnum gameProcessState)
    {
        viewModel.GameProcessState = gameProcessState;
        RefreshViewModel();
    }

    private void RefreshViewModel() => clientViewModelSubject.OnNext(viewModel);
}