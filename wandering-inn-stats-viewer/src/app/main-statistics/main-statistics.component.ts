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
import {OtherBooksComparision, StandardStatisticInterpretation, StatisticsService} from "../statistics.service";
import {VolumeStatistic, WanderingInnStatistic, WritingVelocity} from "../wandering-inn-statistic";
import "billboard.js/dist/theme/insight.css";
import bb, {bar, Chart, ChartOptions, line, radar, spline} from "billboard.js"
import {MatTabGroup} from "@angular/material/tabs";
import {MatCheckbox, MatCheckboxChange} from "@angular/material/checkbox";

@Component({
  selector: 'app-main-statistics',
  templateUrl: './main-statistics.component.html',
  styleUrls: ['./main-statistics.component.scss']
})
export class MainStatisticsComponent implements OnInit, AfterViewInit, OnDestroy {
  chapters: number;
  volumes: number;
  lastVolume: VolumeStatistic;
  statistics: WanderingInnStatistic
  public wordCountVsOthersChartOptionsCached?: ChartOptions;
  public wordCountPerVolumeChartOptionsCached?: ChartOptions;

  standardStatistics: StandardStatisticInterpretation;

  @ViewChild("shittyTabGroup")
  shittyTabGroup: MatTabGroup = null!;

  @ViewChild("filterErin")
  filterErin: MatCheckbox = null!;

  @ViewChild("weightedMode")
  weightedMode: MatCheckbox = null!;

  @ViewChild("shittyTabGroupContainer")
  shittyTabGroupContainer: ElementRef = null!;

  constructor(
    private statisticsService: StatisticsService
  ) {
    this.statistics = statisticsService.getStatistics();
    this.lastVolume = this.statistics.Volumes[this.statistics.Volumes.length - 1];
    this.volumes = this.statistics.Volumes.length;
    this.chapters = this.statistics.Volumes.flatMap(x => x.Chapters).length;
    this.standardStatistics = new StandardStatisticInterpretation(this.statistics, this.statisticsService);
  }

  ngOnInit(): void {
    this.wordCountVsOthersChartOptionsCached = OtherBooksComparision.wordCountVsOthersChartOptions(this.statistics);
    this.wordCountPerVolumeChartOptionsCached = this.wordCountPerVolumeChartOptions();

    window.setTimeout(() => {
      this.fixScrollOfShittyTabs();
    }, 30)
  }

  ngAfterViewInit(): void {

  }

  ngOnDestroy(): void {

  }

  private wordCountPerVolumeChartOptions(): ChartOptions {
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



  handleWeightedChange($event: MatCheckboxChange) {
    if ($event.checked)
      this.shittyTabGroup.selectedIndex = 2;
    else
      this.shittyTabGroup.selectedIndex = this.filterErin.checked ? 1 : 0;
  }

  handleErinFilterChange($event: MatCheckboxChange) {
    if (this.weightedMode.checked)
      return;

    this.shittyTabGroup.selectedIndex = $event.checked ? 1 : 0;
  }

  fixScrollOfShittyTabs() {
    // let div = <HTMLDivElement> this.shittyTabGroupContainer.nativeElement;
    // div.scrollTo({
    //   left: div.clientWidth / 2,
    //   top: 0
    // })
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
    return change && change.currentValue != change.previousValue && !change.firstChange
  }

}

export interface KeyValuePair<T1, T2> {
  key: T1;
  value: T2;
}


