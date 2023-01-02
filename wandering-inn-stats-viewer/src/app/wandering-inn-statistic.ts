export abstract class AbstractWanderingInnStatistic {
  Words: number = 0;
  Characters: number = 0;
  Classes: { [key: string]: number } = {};
  Skills: { [key: string]: number } = {};
  SkillsSpells: { [key: string]: number } = {};
  SkillsSkills: { [key: string]: number } = {};
  CancelledBrackets: { [key: string]: number } = {};
  CharacterMentions: { [key: string]: number } = {};
  ClassObtains: { [key: string]: number } = {};
  ClassMorphs: { [key: string]: number } = {};
  SkillLoss: { [key: string]: number } = {};
  SkillMorphs: { [key: string]: number } = {};
  SkillObtains: { [key: string]: number } = {};
  UnknownBrackets: { [key: string]: number } = {};
  ClassConsolidationRemovals: { [key: string]: number } = {};
  ClassLevelUps: { [key: string]: number } = {};
}

export class WanderingInnStatistic extends AbstractWanderingInnStatistic {
  WritingVelocity!: WritingVelocity;
  Volumes!: VolumeStatistic[];
}

export class VolumeStatistic extends AbstractWanderingInnStatistic {
  Name!: string;
  WritingVelocity!: WritingVelocity;
  Chapters!: ChapterStatistic[];
}

export class ChapterStatistic extends AbstractWanderingInnStatistic {
  Chapter!: ChapterInfo;
}

export class ChapterInfo {
  Volume!: string;
  Url!: string;
  Name!: string;
  Date!: Date;
  Comments!: number;
}

export class WritingVelocity {
  from: Date;
  to: Date;
  words: TimeStatistic;
  characters: TimeStatistic;

  constructor(from: Date, to: Date, words: number, characters: number) {
    this.from = from;
    this.to = to;
    this.words = new TimeStatistic(from, to, words);
    this.characters = new TimeStatistic(from, to, characters);
  }
}

export class TimeStatistic {
  count: number;
  from: Date;
  to: Date;

  constructor(from: Date, to: Date, count: number) {
    this.from = from;
    this.to = to;
    this.count = count;
  }

  private hours(): number {
    let seconds = (this.to.getTime() - this.from.getTime()) / 1000;
    return seconds / (3600);
  }

  perHour(): number {
    return this.count / this.hours();
  }

  perDay(): number {
    return Math.floor(this.count / (this.hours() / 24));
  }
}
