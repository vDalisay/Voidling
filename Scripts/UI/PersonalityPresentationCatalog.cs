using Voidling.Application.Creatures;
using Voidling.Domain.Genetics;

namespace VoidlingGame;

internal static class PersonalityPresentationCatalog
{
    public static string LabelFor(VoidlingPersonalityProfileProjection personality)
    {
        if (personality.Polarity == PersonalityPolarity.Neutral || string.IsNullOrEmpty(personality.TraitId))
            return "Easygoing";

        var high = personality.Polarity == PersonalityPolarity.High;
        return personality.TraitId switch
        {
            PersonalityTraitIds.Curiosity => high ? "Curious" : "Cautious",
            PersonalityTraitIds.Energy => high ? "Energetic" : "Calm",
            PersonalityTraitIds.Naivety => high ? "Trusting" : "Wary",
            PersonalityTraitIds.Appetite => high ? "Food-loving" : "Selective",
            PersonalityTraitIds.Carefree => high ? "Carefree" : "Careful",
            PersonalityTraitIds.Kindness => high ? "Gentle" : "Prickly",
            PersonalityTraitIds.Solitude => high ? "Independent" : "Companionable",
            PersonalityTraitIds.Vitality => high ? "Lively" : "Delicate",
            PersonalityTraitIds.Recovery => high ? "Resilient" : "Sensitive",
            PersonalityTraitIds.Skillfulness => high ? "Precise" : "Clumsy",
            PersonalityTraitIds.Sociability => high ? "Sociable" : "Shy",
            PersonalityTraitIds.Chattiness => high ? "Chatty" : "Quiet",
            PersonalityTraitIds.Fickleness => high ? "Fickle" : "Steady",
            _ => "Easygoing"
        };
    }

    public static string FlavorFor(VoidlingPersonalityProfileProjection personality)
    {
        if (personality.Polarity == PersonalityPolarity.Neutral || string.IsNullOrEmpty(personality.TraitId))
            return "Easygoing and hard to read.";

        var high = personality.Polarity == PersonalityPolarity.High;
        return personality.TraitId switch
        {
            PersonalityTraitIds.Curiosity => high ? "Curious about everything." : "Cautious around unfamiliar things.",
            PersonalityTraitIds.Energy => high ? "Energetic and always ready to move." : "Calm and unhurried.",
            PersonalityTraitIds.Naivety => high ? "Trusting and open-hearted." : "Wary and difficult to fool.",
            PersonalityTraitIds.Appetite => high ? "Especially enthusiastic about food." : "A light and selective eater.",
            PersonalityTraitIds.Carefree => high ? "Carefree and spontaneous." : "Careful and deliberate.",
            PersonalityTraitIds.Kindness => high ? "Gentle and considerate." : "Prickly and strong-willed.",
            PersonalityTraitIds.Solitude => high ? "Independent and comfortable alone." : "Happiest with company nearby.",
            PersonalityTraitIds.Vitality => high ? "Lively and full of spirit." : "Quiet and delicate in its manner.",
            PersonalityTraitIds.Recovery => high ? "Resilient after little setbacks." : "Sensitive to little setbacks.",
            PersonalityTraitIds.Skillfulness => high ? "Precise and methodical." : "Charmingly clumsy at times.",
            PersonalityTraitIds.Sociability => high ? "Sociable and naturally charming." : "Shy around attention.",
            PersonalityTraitIds.Chattiness => high ? "Chatty and expressive." : "Quiet and observant.",
            PersonalityTraitIds.Fickleness => high ? "Fickle and quick to change its mind." : "Steady and consistent.",
            _ => "Easygoing and hard to read."
        };
    }
}
