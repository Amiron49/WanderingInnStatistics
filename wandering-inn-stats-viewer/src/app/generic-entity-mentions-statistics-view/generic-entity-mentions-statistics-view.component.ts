import {Component, Input, OnInit} from '@angular/core';
import {GenericMentionsStatistic} from "./genericMentionsStatistic";
import {ActivatedRoute} from "@angular/router";
import {StatisticsService} from "../statistics.service";
import {AbstractWanderingInnStatistic} from "../wandering-inn-statistic";

@Component({
  selector: 'app-generic-entity-mentions-statistics-view',
  templateUrl: './generic-entity-mentions-statistics-view.component.html',
  styleUrls: ['./generic-entity-mentions-statistics-view.component.scss']
})
export class GenericEntityMentionsStatisticsViewComponent implements OnInit {
  @Input()
  genericEntityStatistic?: GenericMentionsStatistic
  shownColumns = ["Volume", "Chapter"];

  constructor(
    private route: ActivatedRoute,
    private statisticsService: StatisticsService
  ) {

  }

  ngOnInit(): void {
    this.route.params.subscribe(params => {
      let type = params["type"];
      let key = params["key"];
      let locator = GenericEntityMentionsStatisticsViewComponent.createLocator(type)

      this.genericEntityStatistic = new GenericMentionsStatistic(this.statisticsService.getStatistics(), key, locator);
    })
  }

  public static createLocator(entityType: EntityType): (statistic: AbstractWanderingInnStatistic) => { [key: string]: number } {
    switch (entityType) {
      case "class":
        return statistic => statistic.Classes
      case "skill":
        return statistic => statistic.Skills
      case "character":
        return statistic => statistic.CharacterMentions
      case "unknown":
        return statistic => statistic.UnknownBrackets
      default:
        throw new Error(`type ${entityType} is not a known EntityType`)
    }
  }

}

export type EntityType = "class" | "skill" | "character" | "unknown"


