import {NgModule} from '@angular/core';
import {BrowserModule} from '@angular/platform-browser';

import {AppRoutingModule} from './app-routing.module';
import {AppComponent} from './app.component';
import {BrowserAnimationsModule} from '@angular/platform-browser/animations';
import {MatSidenavModule} from "@angular/material/sidenav";
import {MatButtonModule} from "@angular/material/button";
import {MainStatisticsComponent} from './main-statistics/main-statistics.component';
import {ChonkerNumberInfoComponent} from './main-statistics/chonker-number-info/chonker-number-info.component';
import { PrettyPrintNumberPipe } from './main-statistics/chonker-number-info/pretty-print-number.pipe';
import * as d3 from 'd3';
import { SmartChartComponent } from './main-statistics/smart-chart/smart-chart.component';
import { VolumeCharacterOccurenceRadarComponent } from './main-statistics/volume-character-occurence-radar/volume-character-occurence-radar.component';

@NgModule({
  declarations: [
    AppComponent,
    MainStatisticsComponent,
    ChonkerNumberInfoComponent,
    PrettyPrintNumberPipe,
    SmartChartComponent,
    VolumeCharacterOccurenceRadarComponent
  ],
  imports: [
    BrowserModule,
    AppRoutingModule,
    BrowserAnimationsModule,
    MatSidenavModule,
    MatButtonModule
  ],
  providers: [],
  bootstrap: [AppComponent]
})
export class AppModule {
}
