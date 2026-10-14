using System;

[Serializable]
public enum TeachingState
{
    NONE,
    INTRO_PHENOMENON,
    KNOWLEDGE_CARD,
    CHILD_INITIAL_JUDGEMENT,
    AI_CONTROLLED_MISTAKE,
    REFLECTION_PROMPT,
    CHILD_DIRECT_REPLY,
    EVIDENCE_EXPLORE,
    EVIDENCE_SUBMIT,
    AI_SELF_CORRECTION_WITH_EVIDENCE,
    AI_REPAIR,
    FINAL_CONCLUSION
}

[Serializable]
public enum TeachingChoice
{
    NONE,
    CONTINUE,
    INITIAL_JUDGEMENT,
    TRUST_AI,
    DOUBT_AI,
    FIND_EVIDENCE,
    DIRECT_REPLY,
    EVIDENCE_DISCOVERED,
    EVIDENCE_SUBMITTED,
    EVIDENCE_INSUFFICIENT,
    EVIDENCE_VALID
}

[Serializable]
public class TeachingContextSnapshot
{
    public string currentState;
    public string lastChoice;
    public string initialJudgement;
    public string currentKnowledgePointId;
    public string currentEvidenceId;
    public string aiMistakeId;
    public string lastChoiceDetail;
    public System.Collections.Generic.List<string> discoveredEvidenceIds;
    public System.Collections.Generic.List<string> submittedEvidenceIds;
}
