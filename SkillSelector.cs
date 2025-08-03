using System;
using System.Collections.Generic;
using System.Linq;

public class SkillSelector {
    private readonly List<string> skills;
    private readonly List<int> weights;
    private readonly Random rng = new Random();
    private string? lastSelected = null;

    private int retryCount = 0;

    public SkillSelector(Dictionary<string, int> skillWeightDict, int retryCount) {
        skills = skillWeightDict.Keys.ToList();
        weights = skillWeightDict.Values.ToList();
        this.retryCount = retryCount;
    }

    public string GetRandomSkill() {
        string selected = WeightedRandomSelect();
        lastSelected = selected;
        return selected;
    }

    private string WeightedRandomSelect() {
        for (int i = 0; i < retryCount; i++) {
            string selected = SelectOneByWeight();
            if (selected != lastSelected) {
                return selected;
            }
        }
        return lastSelected;
    }

    private string SelectOneByWeight() {
        int totalWeight = weights.Sum();
        int roll = rng.Next(totalWeight);
        int cumulative = 0;

        for (int i = 0; i < skills.Count; i++) {
            cumulative += weights[i];
            if (roll < cumulative) {
                return skills[i];
            }
        }

        // 理论上不会到这里，作为保险
        return skills.Last();
    }
}
