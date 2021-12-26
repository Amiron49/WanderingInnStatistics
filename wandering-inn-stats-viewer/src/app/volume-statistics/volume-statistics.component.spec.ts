import { ComponentFixture, TestBed } from '@angular/core/testing';

import { VolumeStatisticsComponent } from './volume-statistics.component';

describe('VolumeStatisticsComponent', () => {
  let component: VolumeStatisticsComponent;
  let fixture: ComponentFixture<VolumeStatisticsComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      declarations: [ VolumeStatisticsComponent ]
    })
    .compileComponents();
  });

  beforeEach(() => {
    fixture = TestBed.createComponent(VolumeStatisticsComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
