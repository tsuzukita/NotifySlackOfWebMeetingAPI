using FluentValidation;

namespace NotifySlackOfWebMeeting.Apis.Queries
{
    public class WebMeetingsQueryParameterValidator : AbstractValidator<WebMeetingsQueryParameter>
    {
        public WebMeetingsQueryParameterValidator()
        {
            // Web会議の開始日と終了日が指定されている場合、終了日より未来日の開始日はNGとする。
            RuleFor(webMeeting => webMeeting.FromDateUtcValue).LessThanOrEqualTo(webMeeting => webMeeting.ToDateUtcValue).When(webMeeting => webMeeting.ToDate != null).WithMessage("fromDate is invalid. Please specify a date before toDate.");
            // Web会議の開始日と終了日が指定されている場合、開始日より過去日の終了日はNGとする。
            RuleFor(webMeeting => webMeeting.ToDateUtcValue).GreaterThanOrEqualTo(webMeeting => webMeeting.FromDateUtcValue).When(webMeeting => webMeeting.FromDate != null).WithMessage("toDate is invalid. Please specify a date after fromDate.");
        }
    }
}