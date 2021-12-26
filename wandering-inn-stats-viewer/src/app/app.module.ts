import {NgModule} from '@angular/core';
import {BrowserModule} from '@angular/platform-browser';

import {AppRoutingModule} from './app-routing.module';
import {AppComponent} from './app.component';
import {BrowserAnimationsModule} from '@angular/platform-browser/animations';
import {MatSidenavModule} from "@angular/material/sidenav";
import {MatButtonModule} from "@angular/material/button";
import {MainStatisticsComponent} from './main-statistics/main-statistics.component';
import {ChonkerNumberInfoComponent} from './main-statistics/chonker-number-info/chonker-number-info.component';
import {PrettyPrintNumberPipe} from './main-statistics/chonker-number-info/pretty-print-number.pipe';
import * as d3 from 'd3';
import {SmartChartComponent} from './main-statistics/smart-chart/smart-chart.component';
import {
  VolumeCharacterOccurenceRadarComponent
} from './main-statistics/volume-character-occurence-radar/volume-character-occurence-radar.component';
import {MatCheckboxModule} from "@angular/material/checkbox";
import {MatTabsModule} from "@angular/material/tabs";
import {StandardStatisticsViewComponent} from './standard-statistics-view/standard-statistics-view.component';
import {VolumeStatisticsComponent} from './volume-statistics/volume-statistics.component';
import {AppNavigationComponent} from './app-navigation/app-navigation.component';
import {MatListModule} from "@angular/material/list";
import { GenericEntityMentionsStatisticsViewComponent } from './generic-entity-mentions-statistics-view/generic-entity-mentions-statistics-view.component';
import {MatExpansionModule} from "@angular/material/expansion";
import {MatTableModule} from "@angular/material/table";
import {CdkTableModule} from "@angular/cdk/table";

@NgModule({
  declarations: [
    AppComponent,
    MainStatisticsComponent,
    ChonkerNumberInfoComponent,
    PrettyPrintNumberPipe,
    SmartChartComponent,
    VolumeCharacterOccurenceRadarComponent,
    StandardStatisticsViewComponent,
    VolumeStatisticsComponent,
    AppNavigationComponent,
    GenericEntityMentionsStatisticsViewComponent
  ],
    imports: [
        BrowserModule,
        AppRoutingModule,
        BrowserAnimationsModule,
        MatSidenavModule,
        MatButtonModule,
        MatCheckboxModule,
        MatTabsModule,
        MatListModule,
        MatExpansionModule,
        MatTableModule,
        CdkTableModule
    ],
  providers: [],
  bootstrap: [AppComponent]
})
export class AppModule {
}
