import {Component, OnDestroy, OnInit, ViewChild} from '@angular/core';
import {ActivatedRoute, Router} from "@angular/router";
import {StatisticsService} from "../statistics.service";
import {
  EntityType,
  GenericEntityMentionsStatisticsViewComponent
} from "../generic-entity-mentions-statistics-view/generic-entity-mentions-statistics-view.component";
import {FormControl} from "@angular/forms";
import {Subscription} from "rxjs";
import {Helpers, KeyValuePair} from "../main-statistics/main-statistics.component";
import {MatSort, Sort} from "@angular/material/sort";
import {MatTableDataSource} from "@angular/material/table";

@Component({
  selector: 'app-generic-entity-list',
  templateUrl: './generic-entity-list.component.html',
  styleUrls: ['./generic-entity-list.component.scss']
})
export class GenericEntityListComponent implements OnInit, OnDestroy {

  shownColumns = ["value", "key"];

  searchInputChangesSubscription?: Subscription;
  searchInput: FormControl = new FormControl();
  type?: EntityType;

  genericEntities: MatTableDataSource<KeyValuePair<string, number>> = new MatTableDataSource<KeyValuePair<string, number>>();

  constructor(
    private route: ActivatedRoute,
    private router: Router,
    private statisticsService: StatisticsService
  ) {
  }

  @ViewChild(MatSort)
  sort?: MatSort;

  ngAfterViewInit() {
    this.genericEntities.sort = this.sort!;

    this.route.queryParams.subscribe(params => {
      let sortBy = params["sortBy"];
      let sortDirection = params["sortDirection"];

      if (!this.urlChangingProgramatically && sortBy)
      {
        let sort = {
          id: sortBy,
          start: sortDirection,
          disableClear: true
        };
        window.setTimeout(() => this.sort!.sort(sort));
      }
    })
  }

  ngOnInit(): void {
    this.searchInputChangesSubscription = this.searchInput.valueChanges.subscribe(x => {

    })

    this.route.params.subscribe(params => {
      this.type = params["type"];

      let locator = GenericEntityMentionsStatisticsViewComponent.createLocator(this.type!)

      this.genericEntities.data = Helpers.ToDictionary(locator(this.statisticsService.getStatistics()));
    })

    this.route.queryParams.subscribe(params => {
      let query = params["query"];

      if (query && query != this.searchInput.value)
        this.searchInput.setValue(query);
    })
  }

  ngOnDestroy(): void {
    this.searchInputChangesSubscription?.unsubscribe();
  }

  urlChangingProgramatically: boolean = false;

  async onSortChange(sort: Sort) {
    this.urlChangingProgramatically = true;

    await this.router.navigate(
      [],
      {
        relativeTo: this.route,
        queryParams: {
          sortBy: sort.active,
          sortDirection: sort.direction
        },
        queryParamsHandling: 'merge'
      });

    this.urlChangingProgramatically = false;
  }
}
