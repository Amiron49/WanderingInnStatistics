import { ComponentFixture, TestBed } from '@angular/core/testing';

import { StandardStatisticsViewComponent } from './standard-statistics-view.component';

describe('StandardStatisticsViewComponent', () => {
  let component: StandardStatisticsViewComponent;
  let fixture: ComponentFixture<StandardStatisticsViewComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      declarations: [ StandardStatisticsViewComponent ]
    })
    .compileComponents();
  });

  beforeEach(() => {
    fixture = TestBed.createComponent(StandardStatisticsViewComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
