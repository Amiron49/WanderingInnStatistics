import {
  AfterViewInit,
  Component,
  ElementRef,
  HostListener,
  OnDestroy,
  OnInit,
  SimpleChange,
  ViewChild
} from '@angular/core';
import {StatisticsService} from "../statistics.service";
import {VolumeStatistic, WanderingInnStatistic, WritingVelocity} from "../wandering-inn-statistic";
import "billboard.js/dist/theme/insight.css";
import bb, {bar, Chart, ChartOptions, line, radar, spline} from "billboard.js"

@Component({
  selector: 'app-main-statistics',
  templateUrl: './main-statistics.component.html',
  styleUrls: ['./main-statistics.component.scss']
})
export class MainStatisticsComponent implements OnInit, AfterViewInit, OnDestroy {
  chapters: number;
  volumes: number;
  lastVolume: VolumeStatistic;
  pageCount: number;
  statistics: WanderingInnStatistic

  constructor(
    statisticsService: StatisticsService
  ) {
    this.statistics = statisticsService.getStatistics();
    this.pageCount = Math.floor(this.statistics.Characters / 3000);
    this.lastVolume = this.statistics.Volumes[this.statistics.Volumes.length - 1];
    this.volumes = this.statistics.Volumes.length;
    this.chapters = this.statistics.Volumes.flatMap(x => x.Chapters).length;
  }

  ngOnInit(): void {


  }

  ngAfterViewInit(): void {
    this.wordCountVsOthersChartOptions();
  }

  ngOnDestroy(): void {

  }

  createVolumeCharacterMentionsChartOptions(statistics: VolumeStatistic): ChartOptions {
    let topCharacters = Helpers.ToDictionary(statistics.CharacterMentions).filter(Helpers.hackyFilter).sort(Helpers.compare).slice(0, 5);

    let data = [
      ["x", ...topCharacters.map(x => x.key)],
      ["Mentions", ...topCharacters.map(x => x.value)]
    ]

    return {
      data: {
        x: "x",
        columns: <any>data,
        type: radar(),
        labels: {
          colors: "white",
          centered: true
        }
      },
      radar: {
        axis: {
          max: 4500
        },
        level: {
          depth: 5
        },
        direction: {
          clockwise: true
        }
      },
      legend: {
        show: false
      }
    };
  }

  totalCharacterMentionsChartOptions(): ChartOptions {
    let topCharacters = Helpers.ToDictionary(this.statistics.CharacterMentions).filter(Helpers.hackyFilter).sort(Helpers.compare).slice(0, 10);

    let erinFiltered = false;
    let weightedMode = false;

    let data = [
      ["x", ...topCharacters.map(x => x.key)],
      ["Mentions", ...topCharacters.map(x => x.value)]
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
          label: "Mentions"
        },
        x: {
          type: "category"
        }
      },
      legend: {
        show: false
      },
      bar: {
        width: {
          ratio: 0.5
        }
      }
    };
  }

  wordCountPerVolumeChartOptions(): ChartOptions {
    let data = [
      ["x", ...this.statistics.Volumes.map(x => x.Name)],
      ["Words", ...this.statistics.Volumes.map(x => x.Words)],
      ["Words Per Day", ...this.statistics.Volumes.map(x => x.WritingVelocity.words.perDay())]
    ]

    return {
      data: {
        x: "x",
        columns: <any>data,
        type: bar(),
        types: {
          "Words Per Day": line()
        },
        axes: {
          "Words": "y",
          "Words Per Day": "y2",
        },
        labels: {
          colors: "white",
          centered: true,
          format: v => {
            let formatter = new Intl.NumberFormat("en-US");
            return `${formatter.format(v)}`
          }
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
          label: "Words",
          tick: {
            //rotate: 67,
            format: (x: number) => {
              let formatter = new Intl.NumberFormat("en-US");
              return `${formatter.format(x / 1000)}K`
            }
          }
        },
        y2: {
          show: true,
          label: "Words per Day"
        },
        x: {
          type: "category"
        }
      },
      legend: {
        show: false
      },
      bar: {
        width: {
          ratio: 0.5
        }
      }
    };
  }

  private bookWordStats: {
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

  wordCountVsOthersChartOptions(): ChartOptions {
    let generated = this.generateColumnsAndAxisInfos();

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

  private generateColumnsAndAxisInfos(): { xAxis: string[]; columns: any[]; group: string[] } {

    this.statistics.Volumes.forEach(x => {
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

export class Helpers {

  public static hackyFilter(a: KeyValuePair<any, number>):boolean {

    return a.key != "The Putrid One" && a.key != "Goblin Chieftain"
  }

  public static compare(a: KeyValuePair<any, number>, b: KeyValuePair<any, number>) {
    return Helpers.standardCompare(a.value, b.value)
  }

  public static ToDictionary<T>(input: { [key: string]: T }) {
    return Object.entries(input).map(val => {
      return {key: val[0], value: val[1]}
    })
  }

  private static standardCompare(a: number, b: number): number {
    if (a > b)
      return -1;
    else if (a < b)
      return 1;
    return 0;
  }

  public static isChange(change: SimpleChange) {
    return change.currentValue != change.previousValue && !change.firstChange
  }
}

export interface KeyValuePair<T1, T2> {
  key: T1;
  value: T2;
}


