using Ryujinx.Horizon.Common;
using Ryujinx.Horizon.Sdk.Pctl.Detail.Service.Watcher;
using Ryujinx.Horizon.Sdk.Sf;
using Ryujinx.Horizon.Sdk.Time;
using System;

namespace Ryujinx.Horizon.Sdk.Pctl.Detail.Ipc
{
    interface IParentalControlService : IServiceObject
    {
        Result Initialize();
        Result CheckFreeCommunicationPermission();
        Result ConfirmLaunchApplicationPermission(ApplicationId arg0, ReadOnlySpan<sbyte> arg1, bool arg2);
        Result ConfirmResumeApplicationPermission(ApplicationId arg0, ReadOnlySpan<sbyte> arg1, bool arg2);
        Result ConfirmSnsPostPermission();
        Result ConfirmSystemSettingsPermission();
        Result IsRestrictionTemporaryUnlocked(out bool arg0);
        Result RevertRestrictionTemporaryUnlocked();
        Result EnterRestrictedSystemSettings();
        Result LeaveRestrictedSystemSettings();
        Result IsRestrictedSystemSettingsEntered(out bool arg0);
        Result RevertRestrictedSystemSettingsEntered();
        Result GetRestrictedFeatures(out int arg0);
        Result ConfirmStereoVisionPermission();
        Result ConfirmPlayableApplicationVideoOld(ReadOnlySpan<sbyte> arg0);
        Result ConfirmPlayableApplicationVideo(ApplicationId arg0, ReadOnlySpan<sbyte> arg1);
        Result ConfirmShowNewsPermission(ReadOnlySpan<sbyte> arg0);
        Result EndFreeCommunication();
        Result IsFreeCommunicationAvailable();
        Result IsRestrictionEnabled(out bool arg0);
        Result GetSafetyLevel(out int arg0);
        Result SetSafetyLevel(int arg0);
        Result GetSafetyLevelSettings(out RestrictionSettings arg0, int arg1);
        Result GetCurrentSettings(out RestrictionSettings arg0);
        Result SetCustomSafetyLevelSettings(RestrictionSettings arg0);
        Result GetDefaultRatingOrganization(out int arg0);
        Result SetDefaultRatingOrganization(int arg0);
        Result GetFreeCommunicationApplicationListCount(out int arg0);
        Result AddToFreeCommunicationApplicationList(ApplicationId arg0);
        Result DeleteSettings();
        Result GetFreeCommunicationApplicationList(out int arg0, Span<FreeCommunicationApplicationInfo> arg1, int arg2);
        Result UpdateFreeCommunicationApplicationList(ReadOnlySpan<FreeCommunicationApplicationInfo> arg0);
        Result DisableFeaturesForReset();
        Result NotifyApplicationDownloadStarted(ApplicationId arg0);
        Result NotifyNetworkProfileCreated();
        Result ResetFreeCommunicationApplicationList();
        Result ConfirmStereoVisionRestrictionConfigurable();
        Result GetStereoVisionRestriction(out bool arg0);
        Result SetStereoVisionRestriction(bool arg0);
        Result ResetConfirmedStereoVisionPermission();
        Result IsStereoVisionPermitted(out bool arg0);
        Result UnlockRestrictionTemporarily(ReadOnlySpan<sbyte> arg0);
        Result UnlockSystemSettingsRestriction(ReadOnlySpan<sbyte> arg0);
        Result SetPinCode(ReadOnlySpan<sbyte> arg0);
        Result GenerateInquiryCode(out InquiryCode arg0);
        Result CheckMasterKey(out bool arg0, InquiryCode arg1, ReadOnlySpan<sbyte> arg2);
        Result GetPinCodeLength(out int arg0);
        Result GetPinCodeChangedEvent(out int arg0);
        Result GetPinCode(out int arg0, Span<sbyte> arg1);
        Result IsPairingActive(out bool arg0);
        Result GetSettingsLastUpdated(out PosixTime arg0);
        Result GetPairingAccountInfo(out PairingAccountInfoBase arg0, PairingInfoBase arg1);
        Result GetAccountNickname(out uint arg0, Span<sbyte> arg1, PairingAccountInfoBase arg2);
        Result GetAccountState(out int arg0, PairingAccountInfoBase arg1);
        Result RequestPostEvents(out int arg0, Span<EventData> arg1);
        Result GetPostEventInterval(out int arg0);
        Result SetPostEventInterval(int arg0);
        Result GetSynchronizationEvent(out int arg0);
        Result StartPlayTimer();
        Result StopPlayTimer();
        Result IsPlayTimerEnabled(out bool arg0);
        Result GetPlayTimerRemainingTime(out TimeSpanType arg0);
        Result IsRestrictedByPlayTimer(out bool arg0);
        Result GetPlayTimerSettings(out PlayTimerSettings arg0);
        Result GetPlayTimerEventToRequestSuspension(out int arg0);
        Result IsPlayTimerAlarmDisabled(out bool arg0);
        Result NotifyWrongPinCodeInputManyTimes();
        Result CancelNetworkRequest();
        Result GetUnlinkedEvent(out int arg0);
        Result ClearUnlinkedEvent();
        Result DisableAllFeatures(out bool arg0);
        Result PostEnableAllFeatures(out bool arg0);
        Result IsAllFeaturesDisabled(out bool arg0, out bool arg1);
        Result DeleteFromFreeCommunicationApplicationListForDebug(ApplicationId arg0);
        Result ClearFreeCommunicationApplicationListForDebug();
        Result GetExemptApplicationListCountForDebug(out int arg0);
        Result GetExemptApplicationListForDebug(out int arg0, Span<ExemptApplicationInfo> arg1, int arg2);
        Result UpdateExemptApplicationListForDebug(ReadOnlySpan<ExemptApplicationInfo> arg0);
        Result AddToExemptApplicationListForDebug(ApplicationId arg0);
        Result DeleteFromExemptApplicationListForDebug(ApplicationId arg0);
        Result ClearExemptApplicationListForDebug();
        Result DeletePairing();
        Result SetPlayTimerSettingsForDebug(PlayTimerSettings arg0);
        Result GetPlayTimerSpentTimeForTest(out TimeSpanType arg0);
        Result SetPlayTimerAlarmDisabledForDebug(bool arg0);
        Result RequestPairingAsync(out AsyncData arg0, out int arg1, ReadOnlySpan<sbyte> arg2);
        Result FinishRequestPairing(out PairingInfoBase arg0, AsyncData arg1);
        Result AuthorizePairingAsync(out AsyncData arg0, out int arg1, PairingInfoBase arg2);
        Result FinishAuthorizePairing(out PairingInfoBase arg0, AsyncData arg1);
        Result RetrievePairingInfoAsync(out AsyncData arg0, out int arg1);
        Result FinishRetrievePairingInfo(out PairingInfoBase arg0, AsyncData arg1);
        Result UnlinkPairingAsync(out AsyncData arg0, out int arg1, bool arg2);
        Result FinishUnlinkPairing(AsyncData arg0, bool arg1);
        Result GetAccountMiiImageAsync(out AsyncData arg0, out int arg1, out uint arg2, Span<byte> arg3, PairingAccountInfoBase arg4);
        Result FinishGetAccountMiiImage(out uint arg0, Span<byte> arg1, AsyncData arg2);
        Result GetAccountMiiImageContentTypeAsync(out AsyncData arg0, out int arg1, out uint arg2, Span<sbyte> arg3, PairingAccountInfoBase arg4);
        Result FinishGetAccountMiiImageContentType(out uint arg0, Span<sbyte> arg1, AsyncData arg2);
        Result SynchronizeParentalControlSettingsAsync(out AsyncData arg0, out int arg1);
        Result FinishSynchronizeParentalControlSettings(AsyncData arg0);
        Result FinishSynchronizeParentalControlSettingsWithLastUpdated(out PosixTime arg0, AsyncData arg1);
        Result RequestUpdateExemptionListAsync(out AsyncData arg0, out int arg1, ApplicationId arg2, bool arg3);
    }
}