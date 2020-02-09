using MP2BootstrapperApp.Models;
using MP2BootstrapperApp.ViewModels;

namespace MP2BootstrapperApp.WizardSteps
{
  public class ProductExistsStep : IStep
  {
    public void Enter(Wizard wizard, InstallWizardViewModel viewModel, Package model)
    {
      viewModel.Content = new FinishPageViewModel(wizard, model);
      wizard.NextStep = new WelcomeStep();
    }
  }
}
