import {Injectable} from '@angular/core';
import * as fullStatistics from './statistics-frontend.json'
import {VolumeStatistic, WanderingInnStatistic, WritingVelocity} from "./wandering-inn-statistic";

@Injectable({
  providedIn: 'root'
})
export class StatisticsService {

  constructor() {
  }

  getStatistics(): WanderingInnStatistic {
    let stats = <WanderingInnStatistic><any>fullStatistics


    stats.Volumes.flatMap(x => x.Chapters).forEach(x => x.Chapter.Date = new Date(x.Chapter.Date))

    stats.WritingVelocity = this.calculateOverallVelocity(stats);
    stats.Volumes.forEach(x => {
      x.WritingVelocity = this.calculateVolumeVelocity(x);
    })

    return <WanderingInnStatistic><any>fullStatistics;
  }

  private calculateOverallVelocity(stats: WanderingInnStatistic): WritingVelocity {
    let dates = stats.Volumes.flatMap(x => x.Chapters).map(x => x.Chapter.Date.getTime());
    let minDate = Math.min(...dates);
    let maxDate = Math.max(...dates);
    return new WritingVelocity(new Date(minDate), new Date(maxDate), stats.Words, stats.Characters);
  }

  private calculateVolumeVelocity(stats: VolumeStatistic): WritingVelocity {
    let dates = stats.Chapters.map(x => x.Chapter.Date.getTime());
    let minDate = Math.min(...dates);
    let maxDate = Math.max(...dates);
    return new WritingVelocity(new Date(minDate), new Date(maxDate), stats.Words, stats.Characters);
  }
}
