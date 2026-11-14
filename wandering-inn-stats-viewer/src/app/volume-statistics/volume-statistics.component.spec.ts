import {ComponentFixture, TestBed} from '@angular/core/testing';
import {provideRouter} from '@angular/router';

import {VolumeStatisticsComponent} from './volume-statistics.component';
import {StatisticsService} from "../statistics.service";

describe('VolumeStatisticsComponent', () => {
  let component: VolumeStatisticsComponent;
  let fixture: ComponentFixture<VolumeStatisticsComponent>;
  let volumeName: string;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [VolumeStatisticsComponent],
      providers: [provideRouter([])]
    }).compileComponents();

    volumeName = TestBed.inject(StatisticsService).getStatistics().Volumes[0].Name;
    fixture = TestBed.createComponent(VolumeStatisticsComponent);
    component = fixture.componentInstance;
    fixture.componentRef.setInput('volume', volumeName);
    await fixture.whenStable();
  });

  it('shows the volume from the route parameter', () => {
    expect(component.statistics()?.Name).toBe(volumeName);
    expect(fixture.nativeElement.querySelector('h1')?.textContent).toBe(volumeName);
  });

  it('picks the radar tab from the checkboxes', () => {
    expect(component.selectedTab()).toBe(0);
    component.filterErin.set(true);
    expect(component.selectedTab()).toBe(1);
    component.weighted.set(true);
    expect(component.selectedTab()).toBe(2);
  });

  it('shows nothing for an unknown volume', async () => {
    fixture.componentRef.setInput('volume', 'No such volume');
    await fixture.whenStable();
    expect(fixture.nativeElement.querySelector('h1')).toBeNull();
  });
});
