import { ComponentFixture, TestBed } from '@angular/core/testing';

import { AxeGuiComponent } from './axe-gui.component';

describe('AxeGuiComponent', () => {
  let component: AxeGuiComponent;
  let fixture: ComponentFixture<AxeGuiComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [AxeGuiComponent]
    })
    .compileComponents();

    fixture = TestBed.createComponent(AxeGuiComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
