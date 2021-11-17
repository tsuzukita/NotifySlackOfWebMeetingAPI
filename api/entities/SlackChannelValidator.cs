using System;
using FluentValidation;

namespace NotifySlackOfWebMeeting.Apis.Entities
{
    public class SlackChannelValidator : AbstractValidator<SlackChannel>
    {
        public SlackChannelValidator()
        {
            // Slackチャンネル名が未指定の場合はNGとする。
            RuleFor(slackChannel => slackChannel.Name).NotNull().NotEmpty().WithMessage("name is null or empty");
            // SlackチャンネルのWebhookURLが未指定の場合はNGとする。
            RuleFor(slackChannel => slackChannel.WebhookUrl).NotNull().NotEmpty().WithMessage("webhookUrl is null or empty");
            // 登録者が未指定の場合はNGとする。
            RuleFor(slackChannel => slackChannel.RegisteredBy).NotNull().NotEmpty().WithMessage("registeredBy is null or empty");
        }
    }
}