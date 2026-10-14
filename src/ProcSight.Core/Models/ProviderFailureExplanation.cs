namespace ProcSight.Core.Models;
public sealed record ProviderFailureExplanation(string Summary,string Details,string NextStep)
{
    public static ProviderFailureExplanation Describe(ProviderErrorKind kind,ProviderFailureReason? reason=null)
    {
        const string retry="Select this process in Processes and choose Analyze again. If it repeats, choose another model in Settings. No rejected findings or rules were applied.";
        return reason switch {
            ProviderFailureReason.InvalidJson=>new("Provider returned non-JSON output","The response could not be read as the requested structured explanation.",retry),
            ProviderFailureReason.InvalidStructure=>new("Response structure was invalid","Required fields were missing, had incorrect types, or exceeded the accepted structure.",retry),
            ProviderFailureReason.UnknownEvidence=>new("Response cited unsubmitted evidence","The model referred to evidence that was not included in this request. ProcSight rejected that unsupported explanation.",retry),
            ProviderFailureReason.MissingEvidence=>new("Classification lacked supporting evidence","The model made a classification without citing the submitted evidence. Unsupported classifications were rejected.",retry),
            ProviderFailureReason.InvalidAssessment=>new("Classification did not match its category","The model returned an invalid purpose, security or optionality classification.",retry),
            ProviderFailureReason.DuplicateAxes=>new("Response repeated an assessment category","The model returned more than one assessment for the same category.",retry),
            ProviderFailureReason.InvalidRuleIndexes=>new("Proposed rule referred to a missing assessment","The model's rule did not reference a valid returned assessment.",retry),
            ProviderFailureReason.IdentityLabelTooLong=>new("Returned process label was too long","The model's identity label exceeded the 120-character limit.",retry),
            ProviderFailureReason.ThreatEvidenceMissing=>new("Threat claim lacked verified evidence","The model claimed a known threat without exact-hash attributable threat evidence. ProcSight rejected the claim.",retry),
            ProviderFailureReason.StreamEnded=>new("Response ended before completion","The provider stream closed before confirming a complete explanation.","Check your connection and retry the process analysis. Partial output was not applied."),
            ProviderFailureReason.OutputIncomplete=>new("Provider reported incomplete output","The provider did not finish generating the explanation.",retry),
            ProviderFailureReason.OutputTooLarge=>new("Response exceeded the safety limit","The response or stream was larger than ProcSight permits.",retry),
            ProviderFailureReason.NoOutput=>new("Provider returned no explanation","The completed response contained no usable explanation text.",retry),
            _=>kind switch {
                ProviderErrorKind.Auth=>new("Sign-in or API credential required","The selected provider could not authenticate or authorize this request.","Reconnect your ChatGPT account or update the selected API credential in Settings, then save and retry."),
                ProviderErrorKind.RateLimited=>new("Provider usage limit reached","The provider refused more requests because its usage allowance was exhausted or temporarily rate limited.","Check provider usage limits, wait for the allowance to recover, then retry."),
                ProviderErrorKind.Transient=>new("Temporary provider or connection failure","The request could not finish because the provider or connection was temporarily unavailable.","Check your connection and retry. If it persists, check the provider's service status."),
                ProviderErrorKind.Unsupported=>new("Provider does not support this request","The selected endpoint or model rejected the requested analysis format.","Choose a supported model and provider connection in Settings."),
                ProviderErrorKind.Refused=>new("Provider declined this request","The provider returned a refusal rather than an explanation.","Review the selected metadata and provider restrictions before requesting analysis again."),
                ProviderErrorKind.Cancelled=>new("Request cancelled","Analysis was stopped by cancellation or a policy change.","Retry the process analysis when ready."),
                _=>new("Explanation was invalid or incomplete","The response did not pass validation. The specific rejection reason is unavailable for this request.",retry)
            }
        };
    }
}
