import {Component, ElementRef, OnInit, ViewChild} from '@angular/core';
import {AbstractWanderingInnStatistic, VolumeStatistic, WanderingInnStatistic} from "../wandering-inn-statistic";
import {bar, ChartOptions, line} from "billboard.js";
import {OtherBooksComparision, StandardStatisticInterpretation, StatisticsService} from "../statistics.service";
import {MatTabGroup} from "@angular/material/tabs";
import {MatCheckbox, MatCheckboxChange} from "@angular/material/checkbox";
import {ActivatedRoute, Router} from "@angular/router";

@Component({
  selector: 'app-volume-statistics',
  templateUrl: './volume-statistics.component.html',
  styleUrls: ['./volume-statistics.component.scss']
})
export class VolumeStatisticsComponent implements OnInit {

  chapters?: number;
  statistics?: VolumeStatistic
  public wordCountPerChapterChartOptionsCached?: ChartOptions;

  standardStatistics?: StandardStatisticInterpretation;

  @ViewChild("shittyTabGroup")
  shittyTabGroup: MatTabGroup = null!;

  @ViewChild("filterErin")
  filterErin: MatCheckbox = null!;

  @ViewChild("weightedMode")
  weightedMode: MatCheckbox = null!;

  @ViewChild("shittyTabGroupContainer")
  shittyTabGroupContainer: ElementRef = null!;
  doneLoading: boolean = false;

  constructor(
    private route: ActivatedRoute,
    private statisticsService: StatisticsService
  ) {

  }

  ngOnInit(): void {
    this.route.params.subscribe(params => {
      let statistics1 = this.statisticsService.getStatistics();
      this.statistics = statistics1.Volumes.find((x: VolumeStatistic) => x.Name == params["volume"]);
      this.chapters = this.statistics!.Chapters.length;
      this.standardStatistics = new StandardStatisticInterpretation(this.statistics!, this.statisticsService);

      this.wordCountPerChapterChartOptionsCached = this.wordCountPerChapterChartOptions(this.statistics!);
      this.doneLoading = true;
    })
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

  private wordCountPerChapterChartOptions(volumeStats: VolumeStatistic): ChartOptions {
    let data = [
      ["x", ...volumeStats.Chapters.map(x => x.Chapter.Name)],
      ["Words", ...volumeStats.Chapters.map(x => x.Words)]
    ]

    return {
      data: {
        x: "x",
        columns: <any>data,
        type: bar(),
        axes: {
          "Words": "y"
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
}
