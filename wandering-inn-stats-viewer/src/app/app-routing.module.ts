import {NgModule} from '@angular/core';
import {RouterModule, Routes} from '@angular/router';
import {MainStatisticsComponent} from "./main-statistics/main-statistics.component";
import {VolumeStatisticsComponent} from "./volume-statistics/volume-statistics.component";
import {
  GenericEntityMentionsStatisticsViewComponent
} from "./generic-entity-mentions-statistics-view/generic-entity-mentions-statistics-view.component";
import {GenericEntityListComponent} from "./generic-entity-list/generic-entity-list.component";

const routes: Routes = [
  {path: "", component: MainStatisticsComponent},
  {path: "volume/:volume", component: VolumeStatisticsComponent},
  {
    path: ":type",
    children: [
      {path: "", component: GenericEntityListComponent},
      {path: ":key", component: GenericEntityMentionsStatisticsViewComponent}
    ]
  },
];

@NgModule({
  imports: [RouterModule.forRoot(routes)],
  exports: [RouterModule]
})
export class AppRoutingModule {
}
