﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.Foundation.Metadata;
using Windows.Storage;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;
using Microsoft.Services.Store.Engagement;
using Microsoft.Toolkit.Uwp.Connectivity;
using MvpApi.Common.Models;
using MvpApi.Uwp.Dialogs;
using MvpApi.Uwp.Extensions;
using MvpApi.Uwp.Helpers;
using MvpApi.Uwp.Views;
using Template10.Common;
using Template10.Mvvm;
using Template10.Services.NavigationService;
using Template10.Utils;

namespace MvpApi.Uwp.ViewModels
{
    public class AddContributionsViewModel : PageViewModelBase
    {
        #region Fields
        
        private ContributionsModel _selectedContribution;
        private string _urlHeader = "Url";
        private string _annualQuantityHeader = "Annual Quantity";
        private string _secondAnnualQuantityHeader = "Second Annual Quantity";
        private string _annualReachHeader = "Annual Reach";
        private bool _isUrlRequired;
        private bool _isAnnualQuantityRequired;
        private bool _isSecondAnnualQuantityRequired;
        private bool _canUpload = true;
        private bool _isEditingQueuedItem;
        private string _warningMessage;

        #endregion

        public AddContributionsViewModel()
        {
            if (DesignMode.DesignModeEnabled || DesignMode.DesignMode2Enabled)
            {
                Types = DesignTimeHelpers.GenerateContributionTypes();
                Visibilities = DesignTimeHelpers.GenerateVisibilities();
                UploadQueue = DesignTimeHelpers.GenerateContributions();
                SelectedContribution = UploadQueue.FirstOrDefault();

                return;
            }

            EditQueuedContributionCommand = new DelegateCommand<ContributionsModel>(async cont => await EditContribution(cont));
            RemoveQueuedContributionCommand = new DelegateCommand<ContributionsModel>(async cont => await RemoveContribution(cont));

            RemoveAdditionalTechAreaCommand = new DelegateCommand<ContributionTechnologyModel>(RemoveAdditionalArea);

            UploadQueue.CollectionChanged += UploadQueue_CollectionChanged;
        }

        #region Properties

        // Collections and SelectedItems

        public ObservableCollection<ContributionsModel> UploadQueue { get; } = new ObservableCollection<ContributionsModel>();

        public ObservableCollection<ContributionTypeModel> Types { get; } = new ObservableCollection<ContributionTypeModel>();

        public ObservableCollection<VisibilityViewModel> Visibilities { get; } = new ObservableCollection<VisibilityViewModel>();
        
        public ObservableCollection<ContributionAreaContributionModel> CategoryAreas { get; } = new ObservableCollection<ContributionAreaContributionModel>();

        public ContributionsModel SelectedContribution
        {
            get => _selectedContribution;
            set => Set(ref _selectedContribution, value);
        }
        
        // Data entry control headers, using VM properties to alert validation violations
        
        public string AnnualQuantityHeader
        {
            get => _annualQuantityHeader;
            set => Set(ref _annualQuantityHeader, value);
        }

        public string SecondAnnualQuantityHeader
        {
            get => _secondAnnualQuantityHeader;
            set => Set(ref _secondAnnualQuantityHeader, value);
        }

        public string AnnualReachHeader
        {
            get => _annualReachHeader;
            set => Set(ref _annualReachHeader, value);
        }

        public string UrlHeader
        {
            get => _urlHeader;
            set => Set(ref _urlHeader, value);
        }

        public bool IsUrlRequired
        {
            get => _isUrlRequired;
            set => Set(ref _isUrlRequired, value);
        }

        public bool IsAnnualQuantityRequired
        {
            get => _isAnnualQuantityRequired;
            set => Set(ref _isAnnualQuantityRequired, value);
        }

        public bool IsSecondAnnualQuantityRequired
        {
            get => _isSecondAnnualQuantityRequired;
            set => Set(ref _isSecondAnnualQuantityRequired, value);
        }
        
        public bool CanUpload
        {
            get => _canUpload;
            set => Set(ref _canUpload, value);
        }

        public bool IsEditingQueuedItem
        {
            get => _isEditingQueuedItem;
            set => Set(ref _isEditingQueuedItem, value);
        }

        public string WarningMessage
        {
            get => _warningMessage;
            set => Set(ref _warningMessage, value);
        }

        // Commands

        public DelegateCommand<ContributionsModel> EditQueuedContributionCommand { get; set; }

        public DelegateCommand<ContributionsModel> RemoveQueuedContributionCommand { get; set; }

        public DelegateCommand<ContributionTechnologyModel> RemoveAdditionalTechAreaCommand { get; set; }
        
        #endregion
        
        #region Event handlers

        private void UploadQueue_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            CanUpload = UploadQueue.Any();
        }
        
        public async void DatePicker_OnDateChanged(object sender, DatePickerValueChangedEventArgs e)
        {
            if (e.NewDate < new DateTime(2016, 10, 1) || e.NewDate > new DateTime(2019, 4, 1))
            {
                await new MessageDialog("The contribution date must be after the start of your current award period and before April 1st, 2019 in order for it to count towards your evaluation", "Notice: Out of range").ShowAsync();
                WarningMessage = "The contribution date must be after the start of your current award period and before March 31, 2019 in order for it to count towards your evaluation";

                CanUpload = false;
            }
            else
            {
                WarningMessage = "";
                CanUpload = true;
            }
        }
        
        public void ActivityType_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.FirstOrDefault() is ContributionTypeModel type)
            {
                // There are complex rules around the names of the properties, this method determines the requirements and updates the UI accordingly
                DetermineContributionTypeRequirements(type);

                // Also need set the type name
                SelectedContribution.ContributionTypeName = type.EnglishName;
            }
        }

        public async void AdditionalTechnologiesListView_OnItemClick(object sender, ItemClickEventArgs e)
        {
            if (SelectedContribution.AdditionalTechnologies.Count < 2)
            {
                AddAdditionalArea(e.ClickedItem as ContributionTechnologyModel);
            }
            else
            {
                await new MessageDialog("You can only have two additional areas selected, remove one and try again.").ShowAsync();
            }
        }

        // Button click handlers
        public async void AddCurrentItemButton_Click(object sender, RoutedEventArgs e)
        {
            if (!await SelectedContribution.Validate(true))
                return;
            
            if (IsEditingQueuedItem)
            {
                IsEditingQueuedItem = false;
            }
            else
            {
                if(!UploadQueue.Contains(SelectedContribution))
                    UploadQueue.Add(SelectedContribution);
            }
            
            SetupNextEntry();
        }

        public void ClearCurrentItemButton_Click(object sender, RoutedEventArgs e)
        {
            SetupNextEntry();
        }

        public async void ClearQueueButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var md = new MessageDialog("You are about to clear all of the contributions in the upload queue, are you sure?", "CLEAR");
                md.Commands.Add(new UICommand("YES"));
                md.Commands.Add(new UICommand("whoa, no!"));

                var dialogResult = await md.ShowAsync();

                if (dialogResult.Label != "YES")
                    return;

                UploadQueue.Clear();

                SetupNextEntry();
            }
            catch (Exception ex)
            {
                await new MessageDialog($"Something went wrong clearing the queue, please try again. Error: {ex.Message}").ShowAsync();
            }
        }

        public async void UploadQueue_Click(object sender, RoutedEventArgs e)
        {
            foreach (var contribution in UploadQueue)
            {
                contribution.UploadStatus = UploadStatus.InProgress;

                var success = await UploadContributionAsync(contribution);
            
                contribution.UploadStatus = success
                    ? UploadStatus.Success
                    : UploadStatus.Failed;
            }

            UploadQueue.Remove(c => c.UploadStatus == UploadStatus.Success);
            
            if (UploadQueue.Any())
            {
                await new MessageDialog("Not all contributions were saved, view the queue for remaining items and try again", "Incomplete Upload").ShowAsync();

                SelectedContribution = UploadQueue.LastOrDefault();
            }
            else
            {
                if (BootStrapper.Current.NavigationService.CanGoBack)
                    BootStrapper.Current.NavigationService.GoBack();
            }
        }
        
        #endregion

        #region Methods

        private void SetupNextEntry()
        {
            // Set up first contribution, ID is 0 so that we dont accidentally overwrite the data in the form if another contribution is selected for editing
            // this ContributionId will be nulled out before uploading
            SelectedContribution = new ContributionsModel
            {
                ContributionId = 0,
                StartDate = DateTime.Now,
                Visibility = Visibilities.FirstOrDefault(),
                ContributionType = Types.FirstOrDefault(),
                ContributionTypeName = SelectedContribution?.ContributionType?.Name,
                ContributionTechnology = CategoryAreas?.FirstOrDefault()?.ContributionAreas.FirstOrDefault(),
                AdditionalTechnologies = new ObservableCollection<ContributionTechnologyModel>()
            };
        }
        
        public void DetermineContributionTypeRequirements(ContributionTypeModel contributionType)
        {
            switch (contributionType.EnglishName)
            {
                case "Article":
                    AnnualQuantityHeader = "Number of Articles";
                    SecondAnnualQuantityHeader = "";
                    AnnualReachHeader = "Number of Views";
                    IsUrlRequired = false;
                    IsAnnualQuantityRequired = true;
                    IsSecondAnnualQuantityRequired = false;
                    break;
                case "Blog Site Posts":
                    AnnualQuantityHeader = "Number of Posts";
                    SecondAnnualQuantityHeader = "Number of Subscribers";
                    AnnualReachHeader = "Annual Unique Visitors";
                    IsUrlRequired = true;
                    IsAnnualQuantityRequired = true;
                    IsSecondAnnualQuantityRequired = false;
                    break;
                case "Book (Author)":
                    AnnualQuantityHeader = "Number of Books";
                    SecondAnnualQuantityHeader = "";
                    AnnualReachHeader = "Copies Sold";
                    IsUrlRequired = false;
                    IsAnnualQuantityRequired = true;
                    IsSecondAnnualQuantityRequired = false;
                    break;
                case "Book (Co-Author)":
                    AnnualQuantityHeader = "Number of Books";
                    SecondAnnualQuantityHeader = "";
                    AnnualReachHeader = "Copies Sold";
                    IsUrlRequired = false;
                    IsAnnualQuantityRequired = true;
                    IsSecondAnnualQuantityRequired = false;
                    break;
                case "Code Project/Tools":
                    AnnualQuantityHeader = "Number of Projects";
                    SecondAnnualQuantityHeader = "";
                    AnnualReachHeader = "Number of Downloads";
                    IsUrlRequired = true;
                    IsAnnualQuantityRequired = true;
                    IsSecondAnnualQuantityRequired = false;
                    break;
                case "Code Samples":
                    AnnualQuantityHeader = "Number of Samples";
                    SecondAnnualQuantityHeader = "";
                    AnnualReachHeader = "Number of Downloads";
                    IsUrlRequired = true;
                    IsAnnualQuantityRequired = true;
                    IsSecondAnnualQuantityRequired = false;
                    break;
                case "Conference (booth presenter)":
                    AnnualQuantityHeader = "Number of Conferences";
                    SecondAnnualQuantityHeader = "";
                    AnnualReachHeader = "Number of Visitors";
                    IsUrlRequired = false;
                    IsAnnualQuantityRequired = true;
                    IsSecondAnnualQuantityRequired = false;
                    break;
                case "Conference (organizer)":
                    AnnualQuantityHeader = "Number of Conferences";
                    SecondAnnualQuantityHeader = "";
                    AnnualReachHeader = "Number of Visitors";
                    IsUrlRequired = false;
                    IsAnnualQuantityRequired = true;
                    IsSecondAnnualQuantityRequired = false;
                    break;
                case "Forum Moderator":
                    AnnualQuantityHeader = "Number of Threads Moderated";
                    SecondAnnualQuantityHeader = "";
                    AnnualReachHeader = "Annual Reach";
                    IsUrlRequired = false;
                    IsAnnualQuantityRequired = true;
                    IsSecondAnnualQuantityRequired = false;
                    break;
                case "Forum Participation (3rd Party Forums)":
                    AnnualQuantityHeader = "Number of Answers";
                    SecondAnnualQuantityHeader = "Number of Posts";
                    AnnualReachHeader = "Views of Answers";
                    IsUrlRequired = true;
                    IsAnnualQuantityRequired = false;
                    IsSecondAnnualQuantityRequired = true;
                    break;
                case "Forum Participation (Microsoft Forums)":
                    AnnualQuantityHeader = "Number of Answers";
                    SecondAnnualQuantityHeader = "Number of Posts";
                    AnnualReachHeader = "Views of Answers";
                    IsUrlRequired = true;
                    IsAnnualQuantityRequired = true;
                    IsSecondAnnualQuantityRequired = false;
                    break;
                case "Mentorship":
                    AnnualQuantityHeader = "Number of Mentees";
                    SecondAnnualQuantityHeader = "";
                    AnnualReachHeader = "Annual Reach";
                    IsUrlRequired = false;
                    IsAnnualQuantityRequired = true;
                    IsSecondAnnualQuantityRequired = false;
                    break;
                case "Open Source Project(s)":
                    AnnualQuantityHeader = "Project(s)";
                    SecondAnnualQuantityHeader = "";
                    AnnualReachHeader = "Commits";
                    IsUrlRequired = false;
                    IsAnnualQuantityRequired = true;
                    IsSecondAnnualQuantityRequired = false;
                    break;
                case "Other":
                    AnnualQuantityHeader = "Annual Quantity";
                    SecondAnnualQuantityHeader = "";
                    AnnualReachHeader = "Annual Reach";
                    IsUrlRequired = false;
                    IsAnnualQuantityRequired = true;
                    IsSecondAnnualQuantityRequired = false;
                    break;
                case "Product Group Feedback":
                    AnnualQuantityHeader = "Number of Events provided";
                    SecondAnnualQuantityHeader = "";
                    AnnualReachHeader = "Number of Feedbacks provided";
                    IsUrlRequired = false;
                    IsAnnualQuantityRequired = true;
                    IsSecondAnnualQuantityRequired = false;
                    break;
                case "Site Owner":
                    AnnualQuantityHeader = "Posts";
                    SecondAnnualQuantityHeader = "";
                    AnnualReachHeader = "Visitors";
                    IsUrlRequired = false;
                    IsAnnualQuantityRequired = true;
                    IsSecondAnnualQuantityRequired = false;
                    break;
                case "Speaking (Conference)":
                    AnnualQuantityHeader = "Talks";
                    SecondAnnualQuantityHeader = "";
                    AnnualReachHeader = "Attendees of talks";
                    IsUrlRequired = false;
                    IsAnnualQuantityRequired = true;
                    IsSecondAnnualQuantityRequired = false;
                    break;
                case "Speaking (Local)":
                    AnnualQuantityHeader = "Talks";
                    SecondAnnualQuantityHeader = "";
                    AnnualReachHeader = "Attendees of talks";
                    IsUrlRequired = false;
                    IsAnnualQuantityRequired = true;
                    IsSecondAnnualQuantityRequired = false;
                    break;
                case "Speaking (User group)":
                    AnnualQuantityHeader = "Talks";
                    SecondAnnualQuantityHeader = "";
                    AnnualReachHeader = "Attendees of talks";
                    IsUrlRequired = false;
                    IsAnnualQuantityRequired = true;
                    IsSecondAnnualQuantityRequired = false;
                    break;
                case "Technical Social Media (Twitter, Facebook, LinkedIn...)":
                    AnnualQuantityHeader = "Number of Posts";
                    SecondAnnualQuantityHeader = "";
                    AnnualReachHeader = "Number of Followers";
                    IsUrlRequired = true;
                    IsAnnualQuantityRequired = true;
                    IsSecondAnnualQuantityRequired = false;
                    break;
                case "Translation Review, Feedback and Editing":
                    AnnualQuantityHeader = "Annual Quantity";
                    SecondAnnualQuantityHeader = "";
                    AnnualReachHeader = "Annual Reach";
                    IsUrlRequired = false;
                    IsAnnualQuantityRequired = true;
                    IsSecondAnnualQuantityRequired = false;
                    break;
                case "User Group Owner":
                    AnnualQuantityHeader = "Meetings";
                    SecondAnnualQuantityHeader = "Members";
                    AnnualReachHeader = "";
                    IsUrlRequired = false;
                    IsAnnualQuantityRequired = true;
                    IsSecondAnnualQuantityRequired = false;
                    break;
                case "Video":
                    AnnualQuantityHeader = "Number of Videos";
                    SecondAnnualQuantityHeader = "";
                    AnnualReachHeader = "Number of Views";
                    IsUrlRequired = true;
                    IsAnnualQuantityRequired = true;
                    IsSecondAnnualQuantityRequired = false;
                    break;
                case "Webcast":
                    AnnualQuantityHeader = "Number of Videos";
                    SecondAnnualQuantityHeader = "Number of Views";
                    AnnualReachHeader = "";
                    IsUrlRequired = false;
                    IsAnnualQuantityRequired = true;
                    IsSecondAnnualQuantityRequired = false;
                    break;
                case "Website Posts":
                    AnnualQuantityHeader = "Number of Posts";
                    SecondAnnualQuantityHeader = "Number of Subscribers";
                    AnnualReachHeader = "Annual Unique Visitors";
                    IsUrlRequired = true;
                    IsAnnualQuantityRequired = true;
                    IsSecondAnnualQuantityRequired = false;
                    break;
                default: // Fall back on 'other'
                    AnnualQuantityHeader = "Annual Quantity";
                    SecondAnnualQuantityHeader = "";
                    AnnualReachHeader = "Annual Reach";
                    IsUrlRequired = false;
                    IsAnnualQuantityRequired = true;
                    IsSecondAnnualQuantityRequired = false;
                    break;
            }
        }

        private void AddAdditionalArea(ContributionTechnologyModel area)
        {
            if (!SelectedContribution.AdditionalTechnologies.Contains(area))
            {
                SelectedContribution.AdditionalTechnologies.Add(area);
            }
        }

        private void RemoveAdditionalArea(ContributionTechnologyModel area)
        {
            if (SelectedContribution.AdditionalTechnologies.Contains(area))
            {
                SelectedContribution.AdditionalTechnologies.Remove(area);
            }
        }

        #endregion

        #region API calls

        public async Task EditContribution(ContributionsModel contribution)
        {
            if (contribution == null)
                return;

            if (contribution.ContributionId == 0)
            {
                var md = new MessageDialog("Editing this contribution will replace the unsaved information you have in the form. If you do not want to lose that information, click 'cancel' and add it to the queue before editing this one.", "Warning");
                md.Commands.Add(new UICommand("edit"));
                md.Commands.Add(new UICommand("cancel"));

                var dialogResult = await md.ShowAsync();

                if (dialogResult.Label == "cancel")
                    return;
            }

            SelectedContribution = contribution;
            IsEditingQueuedItem = true;
        }

        public async Task RemoveContribution(ContributionsModel contribution)
        {
            if (contribution == null)
                return;

            try
            {
                var md = new MessageDialog("Are you sure you want to remove this contribution from the queue?", "Remove Contribution?");
                md.Commands.Add(new UICommand("yes"));
                md.Commands.Add(new UICommand("cancel"));

                var dialogResult = await md.ShowAsync();

                if (dialogResult.Label == "yes")
                {
                    if (SelectedContribution == contribution)
                    {
                        SelectedContribution = null;
                    }

                    UploadQueue.Remove(contribution);

                    if (UploadQueue.Count == 0)
                    {
                        SetupNextEntry();
                    }
                }
            }
            catch (Exception ex)
            {
                await new MessageDialog($"Something went wrong deleting this item, please try again. Error: {ex.Message}").ShowAsync();
            }
        }

        public async Task<bool> UploadContributionAsync(ContributionsModel contribution)
        {
            try
            {
                var submissionResult = await App.ApiService.SubmitContributionAsync(contribution);

                // copying back the ID which was created on the server once the item was added to the database
                contribution.ContributionId = submissionResult.ContributionId;

                // Quality assurance, only logs a successful upload.
                if (ApiInformation.IsTypePresent("Microsoft.Services.Store.Engagement.StoreServicesCustomEventLogger"))
                    StoreServicesCustomEventLogger.GetDefault().Log("ContributionUploadSuccess");

                return true;
            }
            catch (Exception ex)
            {

                // Quality assurance, only logs a failed upload.
                if (ApiInformation.IsTypePresent("Microsoft.Services.Store.Engagement.StoreServicesCustomEventLogger"))
                    StoreServicesCustomEventLogger.GetDefault().Log("ContributionUploadFailure");

                await new MessageDialog($"Something went wrong saving '{contribution.Title}', it will remain in the queue for you to try again.\r\n\nError: {ex.Message}").ShowAsync();
                return false;
            }
        }

        private async Task LoadSupportingDataAsync()
        {
            IsBusyMessage = "loading types...";

            var types = await App.ApiService.GetContributionTypesAsync();

            types.ForEach(type =>
            {
                Types.Add(type);
            });

            IsBusyMessage = "loading technologies...";

            var areaRoots = await App.ApiService.GetContributionAreasAsync();

            // Flatten out the result so that we only have a single level of grouped data, this is used for the CollectionViewSource, defined in the XAML.
            var areas = areaRoots.SelectMany(areaRoot => areaRoot.Contributions);

            areas.ForEach(area =>
            {
                CategoryAreas.Add(area);
            });


            IsBusyMessage = "loading visibility options...";

            var visibilities = await App.ApiService.GetVisibilitiesAsync();

            visibilities.ForEach(visibility =>
            {
                Visibilities.Add(visibility);
            });
        }

        #endregion

        #region Navigation

        public override async Task OnNavigatedToAsync(object parameter, NavigationMode mode, IDictionary<string, object> state)
        {
            if (!NetworkHelper.Instance.ConnectionInformation.IsInternetAvailable)
            {
                if (BootStrapper.Current.NavigationService.CanGoBack)
                    BootStrapper.Current.NavigationService.GoBack();
            }

            if (ShellPage.Instance.DataContext is ShellPageViewModel shellVm)
            {
                // Verify the user is logged in
                if (!shellVm.IsLoggedIn)
                {
                    IsBusy = true;
                    IsBusyMessage = "logging in...";

                    await ShellPage.Instance.SignInAsync();

                    IsBusyMessage = "";
                    IsBusy = false;
                }

                if (shellVm.IsLoggedIn)
                {
                    try
                    {
                        IsBusy = true;

                        // ** Get the necessary associated data from the API **

                        await LoadSupportingDataAsync();

                        SetupNextEntry();

                        if (!(ApplicationData.Current.LocalSettings.Values["AddContributionPageTutorialShown"] is bool tutorialShown) || !tutorialShown)
                        {
                            var td = new TutorialDialog
                            {
                                SettingsKey = "AddContributionPageTutorialShown",
                                MessageTitle = "Add Contribution Page",
                                Message = "This page allows you to add contributions to your MVP profile.\r\n\n" +
                                          "- Complete the form and the click the 'Add' button to add the completed contribution to the upload queue.\r\n" +
                                          "- You can edit or remove items that are already in the queue using the item's 'Edit' or 'Remove' buttons.\r\n" +
                                          "- Click 'Upload' to save the queue to your profile.\r\n" +
                                          "- You can clear the form, or the entire queue, using the 'Clear' buttons.\r\n\n" +
                                          "TIP: Watch the queue items color change as the items are uploaded and save is confirmed."
                            };

                            await td.ShowAsync();
                        }

                        // To prevent accidental back navigation
                        if (BootStrapper.Current.NavigationService.FrameFacade != null)
                            BootStrapper.Current.NavigationService.FrameFacade.BackRequested += FrameFacadeBackRequested;
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"AddContributions OnNavigatedToAsync Exception {ex}");
                    }
                    finally
                    {
                        IsBusyMessage = "";
                        IsBusy = false;
                    }
                }
            }
        }
        

        public override Task OnNavigatingFromAsync(NavigatingEventArgs args)
        {
            if (BootStrapper.Current.NavigationService.FrameFacade != null)
                BootStrapper.Current.NavigationService.FrameFacade.BackRequested -= FrameFacadeBackRequested;
            
            return base.OnNavigatingFromAsync(args);
        }

        // Prevent back key press. Credit Daren May https://github.com/Windows-XAML/Template10/issues/737
        private async void FrameFacadeBackRequested(object sender, HandledEventArgs e)
        {
            try
            {
                e.Handled = CanUpload;

                if (CanUpload)
                {
                    var md = new MessageDialog("Navigating away now will lose your pending uploads, continue?", "Warning: Pending Uploads");
                    md.Commands.Add(new UICommand("yes"));
                    md.Commands.Add(new UICommand("no"));
                    md.CancelCommandIndex = 1;
                    md.DefaultCommandIndex = 1;

                    var result = await md.ShowAsync();

                    if (result.Label == "yes")
                    {
                        if (NavigationService.CanGoBack)
                        {
                            NavigationService.GoBack();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                await ex.LogExceptionAsync();
            }
        }

        #endregion
    }
}