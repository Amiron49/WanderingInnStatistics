import {Injectable} from '@angular/core';
import fullStatistics from './statistics-frontend.json'
import definitions from './definitions-frontend.json'
import {
  AbstractWanderingInnStatistic, ChapterInfo,
  VolumeStatistic,
  WanderingInnStatistic,
  WritingVelocity
} from "./wandering-inn-statistic";
import {Helpers, KeyValuePair} from "./main-statistics/main-statistics.component";
import {bar, ChartOptions} from "billboard.js";

@Injectable({
  providedIn: 'root'
})
export class StatisticsService {

  constructor() {
  }

  private cached!: WanderingInnStatistic;

  getStatistics(): WanderingInnStatistic {
    if (this.cached)
      return this.cached;

    let stats = <WanderingInnStatistic><any>fullStatistics

    stats.Volumes.flatMap(x => x.Chapters).forEach(x => x.Chapter.Date = new Date(x.Chapter.Date))

    stats.WritingVelocity = this.calculateOverallVelocity(stats);
    stats.Volumes.forEach(x => {
      x.WritingVelocity = this.calculateVolumeVelocity(x);
    })

    this.cached = stats;

    return stats;
  }

  private calculateOverallVelocity(stats: WanderingInnStatistic): WritingVelocity {
    let dates = stats.Volumes.flatMap(x => x.Chapters).map(x => x.Chapter.Date.getTime());
    let minDate = Math.min(...dates);
    let maxDate = Math.max(...dates);
    return new WritingVelocity(new Date(minDate), new Date(maxDate), stats.Words, stats.Characters);
  }

  private calculateVolumeVelocity(stats: VolumeStatistic): WritingVelocity {
    let dates = stats.Chapters.map(x => x.Chapter.Date.getTime());
    let minDate = Math.min(...dates);
    let maxDate = Math.max(...dates);
    return new WritingVelocity(new Date(minDate), new Date(maxDate), stats.Words, stats.Characters);
  }

  public spellFilter(kvp: KeyValuePair<string, number>): boolean {
    return Array.from(definitions.Spells).indexOf(kvp.key) >= 0;
  }

  public skillFilter(kvp: KeyValuePair<string, number>): boolean {
    return Array.from(definitions.Skills).indexOf(kvp.key) >= 0;
  }

  public chaptersMentioningSkill(name: string): ChapterInfo[] | null {
    //
    return null;
  }
}

export class StandardStatisticInterpretation {
  public pageCount: number;
  public totalCharacterMentionsChartOptions: ChartOptions;
  public uniqueCharacters: number;
  public uniqueClasses: number;
  public uniqueSkills: number;
  public uniqueSpells: number;
  public uniqueUnknowns: number;
  public charactersOnlyMentionedOnce: number;

  public topClassesChartOptions: ChartOptions;
  public topSkillsChartOptions: ChartOptions;
  public topSpellsChartOptions: ChartOptions;
  public mostMentionedClass: KeyValuePair<string, number>;
  public classesOnlyUsedOnce: number;
  public mostUsedSkill: KeyValuePair<string, number>;
  public amountOfSkillsOnlyUsedOnce: number;
  public mostUsedSpell: KeyValuePair<string, number>;
  public amountOfSpellsOnlyUsedOnce: number;
  public erinMentions: number;

  public topUnknownsChartOptions: ChartOptions;

  public topMostCommentedOnChapterChartOptions?: ChartOptions;
  public mostCommentedOnChapter?: ChapterInfo;
  public leastCommentedOnChapter?: ChapterInfo;

  constructor(private statistics: AbstractWanderingInnStatistic, private statisticsService: StatisticsService) {
    this.totalCharacterMentionsChartOptions = this.createTotalCharacterMentionsChartOptions();
    this.charactersOnlyMentionedOnce = Helpers.ToDictionary(statistics.CharacterMentions).filter(x => x.value == 0).length;
    this.uniqueCharacters = Object.entries(statistics.CharacterMentions).length;

    let classes = Helpers.ToDictionary(this.statistics.Classes);
    this.mostMentionedClass = classes.sort(Helpers.compare)[0];
    this.topClassesChartOptions = StandardStatisticInterpretation.createKVPMentionBasedBarChart(classes, 10)
    this.uniqueClasses = classes.length;
    this.classesOnlyUsedOnce = classes.filter(x => x.value == 1).length;

    let skills = Helpers.ToDictionary(this.statistics.Skills).filter(statisticsService.skillFilter);
    this.mostUsedSkill = skills.sort(Helpers.compare)[0];
    this.topSkillsChartOptions = StandardStatisticInterpretation.createKVPMentionBasedBarChart(skills, 10)
    this.amountOfSkillsOnlyUsedOnce = skills.filter(x => x.value == 1).length;
    this.uniqueSkills = skills.length;

    let spells = Helpers.ToDictionary(this.statistics.Skills).filter(statisticsService.spellFilter);
    this.topSpellsChartOptions = StandardStatisticInterpretation.createKVPMentionBasedBarChart(spells, 10)
    this.mostUsedSpell = spells.sort(Helpers.compare)[0];
    this.amountOfSpellsOnlyUsedOnce = spells.filter(x => x.value == 1).length;
    this.uniqueSpells = spells.length;

    let unknowns = Helpers.ToDictionary(this.statistics.UnknownBrackets);
    this.topUnknownsChartOptions = StandardStatisticInterpretation.createKVPMentionBasedBarChart(unknowns, 10);
    this.uniqueUnknowns = unknowns.length;

    this.pageCount = Math.floor(this.statistics.Characters / 3000);
    this.erinMentions = this.statistics.CharacterMentions["Erin Solstice"];

    if ((<WanderingInnStatistic>statistics).Volumes)
    {
      let volumes = (<WanderingInnStatistic>statistics).Volumes;
      let chapterInfos = volumes.flatMap(x => x.Chapters.flatMap(chapter => chapter.Chapter));
      console.log(chapterInfos);
      this.createChapterInfoBasedStatistics(chapterInfos);
    } else if ((<VolumeStatistic>statistics).Chapters) {
      this.createChapterInfoBasedStatistics((<VolumeStatistic>statistics).Chapters.flatMap(chapter => chapter.Chapter));
    }
  }

  private createChapterInfoBasedStatistics(chapterInfos: ChapterInfo[], chapterNameFunc: (chapterInfo: ChapterInfo) => string = chapterInfo => chapterInfo.Name): void {
    let chapterByComments = chapterInfos.map(chapter => <KeyValuePair<ChapterInfo, number>> {
      key: chapter,
      value: chapter.Comments
    }).sort(Helpers.compare);

    this.mostCommentedOnChapter = chapterByComments[0].key;
    this.leastCommentedOnChapter = chapterByComments[chapterByComments.length - 1].key;

    this.topMostCommentedOnChapterChartOptions = StandardStatisticInterpretation.createKVPMentionBasedBarChart(chapterByComments.map(x => <KeyValuePair<string, number>>{
      key: chapterNameFunc(x.key),
      value: x.value
    }),10, "Comments");
  }

  private createTotalCharacterMentionsChartOptions(): ChartOptions {
    let characters = Helpers.ToDictionary(this.statistics.CharacterMentions).filter(Helpers.hackyFilter);
    return StandardStatisticInterpretation.createKVPMentionBasedBarChart(characters, 10)
  }

  private static createKVPMentionBasedBarChart(thingies: KeyValuePair<string, number>[], amount: number, yAxisLabel = "Mentions"): ChartOptions {
    let dataData = thingies.sort(Helpers.compare).slice(0, amount);

    let data = [
      ["x", ...dataData.map(x => x.key)],
      [yAxisLabel, ...dataData.map(x => x.value)]
    ]

    return {
      data: {
        x: "x",
        columns: <any>data,
        type: bar(),
        labels: {
          colors: "white",
          centered: true
        }
      },
      grid: {
        y: {
          lines: [
            {
              value: 0
            }
          ],
        }
      },
      axis: {
        y: {
          show: true,
          label: yAxisLabel
        },
        x: {
          type: "category"
        }
      },
      legend: {
        // show: true,
        show: false
      },
      bar: {
        width: {
          ratio: 0.5
        }
      }
    };
  }
}

export class OtherBooksComparision {


  private static bookWordStats: {
    [key: string]: (string | number)[][]
  } = {
    "Wandering Inn": [],
    "Wheel of Time": [
      ["Eye of the World", 305902],
      ["The Great Hunt", 267078],
      ["The Dragon Reborn", 251392],
      ["The Shadow Rising", 393823],
      ["The Fires of Heaven", 354109],
      ["Lord of Chaos", 389823],
      ["A Crown of Swords", 295028],
      ["The Path of Daggers", 226687],
      ["Winter’s Heart", 238789],
      ["Crossroads of Twilight", 271632],
      ["Knife of Dreams", 315163],
      ["The Gathering Storm", 297502],
      ["Towers of Midnight", 327052],
      ["A Memory of Light", 353906]
    ],
    "Game of Thrones": [
      ["A Game of Thrones", 292727],
      ["A Clash of Kings", 318903],
      ["A Storm of Swords", 414604],
      ["A Feast for Crows", 295032],
      ["A Dance with Dragons", 414788]
    ],
    "Harry Potter": [
      ["Philosopher's Stone", 76944],
      ["Chamber of Secrets", 85141],
      ["Prisoner of Azkaban", 107253],
      ["Goblet of Fire", 190637],
      ["Order of the Phoenix", 257045],
      ["Half-Blood Prince", 168923],
      ["Deathly Hallows", 198227]
    ],
    "Lord of the Rings": [
      ["The Fellowship of the Ring", 177227],
      ["The Two Towers", 143436],
      ["The Return of the King", 134462]
    ]
  }

  public static wordCountVsOthersChartOptions(statistics: WanderingInnStatistic): ChartOptions {
    let generated = this.generateColumnsAndAxisInfos(statistics);

    return {
      data: {
        x: "x",
        columns: generated.columns,
        type: bar(),
        groups: [
          generated.group
        ],
        order: (a, b) => {
          let numberRegex = /\d+/;

          let numberA = Number.parseInt(numberRegex.exec(a.id)![0])
          let numberB = Number.parseInt(numberRegex.exec(b.id)![0])

          if (numberA > numberB)
            return 1;

          return -1;
        },
        labels: {
          colors: "white",
          centered: true,
          format: (v, id, index, j) => {
            if (v == 0)
              return "";

            return id;

            if (!id)
              return "wtf";

            let fixedIndex = generated.columns.findIndex((x: [string | any]) => x[0] === id) - 1;
            const subIndex = index;

            let story = generated.xAxis[subIndex];
            let storyVolumeNames = this.bookWordStats[story];
            try {
              let names = storyVolumeNames[fixedIndex];

              if (!names)
                return "MISSING NAME"

              return names[0];
            } catch (e) {
              throw `Cannot find volume name for ${story} @ ${fixedIndex}`
            }
          }
        }
      },
      grid: {
        y: {
          lines: [
            {
              value: 0
            }
          ]
        }
      },
      axis: {
        rotated: true,
        y: {
          show: true,
          label: "Words",
          tick: {
            //rotate: 67,
            format: (x: number) => {
              let formatter = new Intl.NumberFormat("en-US");
              return `${formatter.format(x / 1000)}K`
            }
          }
        },
        x: {
          type: "category",

        }
      },
      legend: {
        show: false
      },
      bar: {
        width: {
          ratio: 0.5
        }
      },
      tooltip: {
        grouped: true,
        format: {
          name: (name, ratio, id, index): string => {

            let fixedIndex = generated.columns.findIndex((x: [string | any]) => x[0] === id) - 1;
            const subIndex = index;

            let story = generated.xAxis[subIndex];
            let storyVolumeNames = this.bookWordStats[story];
            try {
              let names = storyVolumeNames[fixedIndex];

              if (!names)
                return "MISSING NAME"

              return id + ": " + <string>names[0];
            } catch (e) {
              throw `Cannot find volume name for ${story} @ ${fixedIndex}`
            }
          },
          value: (value, ratio, id, index): string => {
            if (value == 0)
              return <any>undefined

            return value;
          }
        }
      }
    }
  }

  private static generateColumnsAndAxisInfos(statistics: WanderingInnStatistic): { xAxis: string[]; columns: any[]; group: string[] } {

    this.bookWordStats["Wandering Inn"] = [];

    statistics.Volumes.forEach(x => {
      this.bookWordStats["Wandering Inn"].push([x.Name, x.Words])
    })

    //let xAxis = ["Wandering Inn", "Harry Potter"];
    let xAxis = Object.keys(this.bookWordStats);

    let asSeriesArray = Object.values(this.bookWordStats);
    let asVolumeCounts = Object.values(this.bookWordStats).map(series => series.length);
    let mostVolumes = Math.max(...asVolumeCounts);

    let columns: (any)[] = [
      ["x", ...xAxis],
    ];

    let group: string[] = [];

    for (let volumeIndex = 0; volumeIndex < mostVolumes; volumeIndex++) {
      let dataColumn: (string | number)[] = [`V${volumeIndex + 1}`];
      group.push(`V${volumeIndex + 1}`);

      asSeriesArray.forEach(series => {
        let volumeInfo = series[volumeIndex];

        if (volumeInfo)
          dataColumn.push(series[volumeIndex][1]);
        else
          dataColumn.push(0);
      })

      columns.push(dataColumn)
    }

    return {
      xAxis: xAxis,
      columns: columns,
      group: group,
    }
  }
}
