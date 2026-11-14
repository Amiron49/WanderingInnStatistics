import {ChangeDetectionStrategy, Component, computed, inject, input} from '@angular/core';
import {GenericMentionsStatistic} from "./genericMentionsStatistic";
import {StatisticsService} from "../statistics.service";
import {AbstractWanderingInnStatistic} from "../wandering-inn-statistic";
import {SmartChartComponent} from '../main-statistics/smart-chart/smart-chart.component';
import {
  MatCell,
  MatColumnDef,
  MatHeaderCell,
  MatHeaderCellDef,
  MatHeaderRow,
  MatHeaderRowDef,
  MatRow,
  MatRowDef,
  MatTable
} from '@angular/material/table';
import {CdkCellDef} from '@angular/cdk/table';

/** Mentions of one entity per volume, and every chapter that mentions it. */
@Component({
  selector: 'app-generic-entity-mentions-statistics-view',
  templateUrl: './generic-entity-mentions-statistics-view.component.html',
  styleUrls: ['./generic-entity-mentions-statistics-view.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [SmartChartComponent, MatTable, MatColumnDef, MatHeaderCellDef, MatHeaderCell, CdkCellDef, MatCell, MatHeaderRowDef, MatHeaderRow, MatRowDef, MatRow]
})
export class GenericEntityMentionsStatisticsViewComponent {
  private readonly statisticsService = inject(StatisticsService);

  /** Route parameters <c>:type</c> and <c>:key</c>. */
  readonly type = input.required<EntityType>();
  readonly key = input.required<string>();

  readonly shownColumns = ["Volume", "Chapter"];

  readonly genericEntityStatistic = computed(() =>
    new GenericMentionsStatistic(this.statisticsService.getStatistics(), this.key(),
      GenericEntityMentionsStatisticsViewComponent.createLocator(this.type())));

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
      case "species":
        return statistic => statistic.SpeciesMentions ?? {}
      case "monster":
        return statistic => statistic.MonsterMentions ?? {}
      case "location":
        return statistic => statistic.LocationMentions ?? {}
      default:
        throw new Error(`type ${entityType} is not a known EntityType`)
    }
  }

}

export type EntityType = "class" | "skill" | "character" | "unknown" | "species" | "monster" | "location"


