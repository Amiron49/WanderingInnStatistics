import {ChangeDetectionStrategy, Component, computed, effect, inject, input, viewChild} from '@angular/core';
import {ActivatedRoute, Router, RouterLink} from "@angular/router";
import {StatisticsService} from "../statistics.service";
import {
  EntityType,
  GenericEntityMentionsStatisticsViewComponent
} from "../generic-entity-mentions-statistics-view/generic-entity-mentions-statistics-view.component";
import {Helpers, KeyValuePair} from "../main-statistics/main-statistics.component";
import {MatSort, MatSortHeader, Sort, SortDirection} from "@angular/material/sort";
import {
  MatCell,
  MatColumnDef,
  MatHeaderCell,
  MatHeaderCellDef,
  MatHeaderRow,
  MatHeaderRowDef,
  MatRow,
  MatRowDef,
  MatTable,
  MatTableDataSource
} from "@angular/material/table";
import {CdkCellDef} from '@angular/cdk/table';

/** All entities of one type with their mention counts; the sort is kept in the url. */
@Component({
  selector: 'app-generic-entity-list',
  templateUrl: './generic-entity-list.component.html',
  styleUrls: ['./generic-entity-list.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MatTable, MatSort, MatColumnDef, MatHeaderCellDef, MatHeaderCell, MatSortHeader, CdkCellDef, MatCell, RouterLink, MatHeaderRowDef, MatHeaderRow, MatRowDef, MatRow]
})
export class GenericEntityListComponent {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly statisticsService = inject(StatisticsService);

  /** Route parameter <c>:type</c>. */
  readonly type = input.required<EntityType>();
  /** Query parameters. */
  readonly sortBy = input<string>();
  readonly sortDirection = input<SortDirection>();

  readonly shownColumns = ["value", "key"];

  private readonly sort = viewChild(MatSort);

  readonly genericEntities = computed(() => {
    const locator = GenericEntityMentionsStatisticsViewComponent.createLocator(this.type());
    return new MatTableDataSource<KeyValuePair<string, number>>(Helpers.ToDictionary(locator(this.statisticsService.getStatistics())));
  });

  constructor() {
    // Hooks the table up to the sort header and applies the sort from the url (on load and on back/forward).
    effect(() => {
      const dataSource = this.genericEntities();
      const sort = this.sort();
      if (!sort)
        return;

      dataSource.sort = sort;

      const active = this.sortBy() ?? "";
      const direction = this.sortDirection() ?? "";
      if (sort.active !== active || sort.direction !== direction) {
        sort.active = active;
        sort.direction = direction;
        sort.sortChange.emit({active, direction});
      }
    });
  }

  async onSortChange(sort: Sort) {
    await this.router.navigate([], {
      relativeTo: this.route,
      queryParams: {
        sortBy: sort.active,
        sortDirection: sort.direction
      },
      queryParamsHandling: 'merge'
    });
  }
}
